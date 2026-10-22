using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

// Versions, reviews and the publication pointer share one document for atomic transitions.
public sealed class ResourceService(MongoContext db, FileStorageService files, UserService users,
    NotificationService notifications, LearningEventService events)
{
    public async Task<LearningResource?> GetAsync(string id)
    {
        if (!ObjectId.TryParse(id, out var key)) return null;
        return await db.Resources.Find(x => x.Id == key).FirstOrDefaultAsync();
    }

    public async Task<List<Subject>> AuthorSubjectsAsync(User user) => await db.Subjects.Find(x => !x.IsArchived &&
        (user.Role == Roles.Admin || user.TeachingSubjectIds.Contains(x.Id))).SortBy(x => x.Name).ToListAsync();

    private async Task<bool> CanManageAsync(User user, LearningResource resource) => user.Role == Roles.Admin ||
        (user.Role == Roles.Teacher && resource.OwnerId == user.Id && await users.HasPermissionAsync(user.Id, PermissionCodes.ResourcesManage));

    private async Task<bool> CanReviewAsync(User user, ResourceVersion version)
    {
        if (user.Role == Roles.Admin) return true;
        if (user.Role != Roles.Staff || user.DepartmentId is not { } department ||
            !await users.HasPermissionAsync(user.Id, PermissionCodes.ResourcesReview) ||
            !await db.Departments.Find(x => x.Id == department && !x.IsArchived).AnyAsync()) return false;
        return await db.Subjects.CountDocumentsAsync(x => version.SubjectIds.Contains(x.Id) &&
            x.DepartmentId == department && !x.IsArchived) == version.SubjectIds.Count;
    }

    private async Task<bool> CanReadPublishedAsync(User user, ResourceVersion version)
    {
        if (version.Access == "School") return true;
        var activeSubjects = await db.Subjects.Find(x => version.SubjectIds.Contains(x.Id) && !x.IsArchived).ToListAsync();
        var subjectIds = activeSubjects.Select(x => x.Id).ToArray();
        if (user.Role == Roles.Teacher) return subjectIds.Any(user.TeachingSubjectIds.Contains);
        if (user.Role == Roles.Staff) return user.DepartmentId is { } department &&
            await db.Departments.Find(x => x.Id == department && !x.IsArchived).AnyAsync() &&
            activeSubjects.Any(x => x.DepartmentId == department);
        return user.Role == Roles.Student && await db.Classes.Find(x => !x.IsArchived &&
            subjectIds.Contains(x.SubjectId) && x.StudentIds.Contains(user.Id)).AnyAsync();
    }

    public async Task<ResourceVersion?> VisibleVersionAsync(User user, LearningResource resource, int? versionNumber = null)
    {
        var version = versionNumber.HasValue ? resource.Versions.FirstOrDefault(x => x.Number == versionNumber) : resource.Versions.LastOrDefault();
        if (version is null) return null;
        if (await CanManageAsync(user, resource)) return version;
        if (resource.IsArchived) return null;
        if (version.State != ResourceStates.Draft && await CanReviewAsync(user, version)) return version;
        var published = resource.Versions.FirstOrDefault(x => x.Number == resource.PublishedVersion);
        if (published is null || (versionNumber.HasValue && versionNumber != resource.PublishedVersion)) return null;
        return await CanReadPublishedAsync(user, published) ? published : null;
    }

    public async Task<ResourceResponse> ToResponseAsync(User user, LearningResource resource, ResourceVersion version)
    {
        var manager = await CanManageAsync(user, resource);
        var reviewer = await CanReviewAsync(user, version);
        var latest = resource.Versions.Last().Number == version.Number;
        var subjects = await db.Subjects.Find(x => version.SubjectIds.Contains(x.Id)).ToListAsync();
        var history = new List<ResourceHistoryResponse>();
        if (manager || reviewer)
            foreach (var v in resource.Versions)
                if (manager || (v.State != ResourceStates.Draft && await CanReviewAsync(user, v)))
                    history.Add(new(v.Number, v.Title, v.State, v.CreatedAt, v.ReviewerName, v.ReviewedAt, v.ReviewComment));
        return new(resource.Id.ToString(), manager || reviewer ? resource.Revision : 0, version.Number,
            version.Title, version.Description, version.Category, version.AuthorName, version.Source, version.License,
            version.UsageTerms, version.Access, version.ExternalUrl, version.SubjectIds.Select(x => x.ToString()).ToArray(),
            subjects.Select(x => x.Name).ToArray(), version.Files.Select(x => new ApiFileResponse(x.Id.ToString(), x.FileName, x.Length)).ToArray(),
            version.State, resource.IsArchived, manager && latest && !resource.IsArchived && version.State != ResourceStates.Pending,
            reviewer && latest && !resource.IsArchived && version.State == ResourceStates.Pending && resource.OwnerId != user.Id && version.EditorId != user.Id,
            manager && latest && !resource.IsArchived && version.State == ResourceStates.Approved,
            manager && latest && !resource.IsArchived && version.State == ResourceStates.Draft,
            manager && latest && !resource.IsArchived,
            manager || reviewer ? version.ReviewComment : "", history);
    }

    public async Task<object> ListAsync(User user, string? query, string? category, string? license, string? access,
        string? subjectId, string? state, int page)
    {
        // Small personal-project catalog: authorize before filtering, counting and paging.
        var matches = new List<ResourceResponse>();
        foreach (var resource in await db.Resources.Find(_ => true).SortByDescending(x => x.UpdatedAt).ToListAsync())
        {
            var version = await VisibleVersionAsync(user, resource);
            if (version is null || (!string.IsNullOrWhiteSpace(query) &&
                !(version.Title + " " + version.AuthorName + " " + version.Description).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(category) && !version.Category.Equals(category, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(license) && !version.License.Contains(license.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(access) && !version.Access.Equals(access, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(subjectId) && !version.SubjectIds.Any(x => x.ToString() == subjectId)) ||
                (!string.IsNullOrWhiteSpace(state) && (resource.IsArchived ? "Archived" : version.State) != state)) continue;
            matches.Add(await ToResponseAsync(user, resource, version));
        }
        return new { items = matches.Skip((page - 1) * 12).Take(12), total = matches.Count, page, pageSize = 12 };
    }

    private async Task<ResourceVersion> BuildVersionAsync(User user, ResourceRequest request, LearningResource? resource)
    {
        if (request.SubjectIds is null || request.FileIds is null) throw new ArgumentException("Danh sách môn/tệp không hợp lệ.");
        var subjects = await AuthorSubjectsAsync(user);
        var requestedSubjects = request.SubjectIds.Distinct().ToArray();
        if (requestedSubjects.Length == 0 || requestedSubjects.Any(id => !subjects.Any(s => s.Id.ToString() == id)))
            throw new ArgumentException("Chỉ được gắn tài liệu với môn học đang được phân công.");
        if (!string.IsNullOrWhiteSpace(request.ExternalUrl) &&
            (!Uri.TryCreate(request.ExternalUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https")))
            throw new ArgumentException("Đường dẫn học liệu phải dùng http hoặc https.");
        var ids = FileStorageService.ParseIds(string.Join(",", request.FileIds));
        if (ids is null || ids.Count != request.FileIds.Length || ids.Count > 5) throw new ArgumentException("Danh sách tệp không hợp lệ.");
        var retained = (resource?.Versions.SelectMany(x => x.Files) ?? Enumerable.Empty<StoredFileReference>())
            .DistinctBy(x => x.Id).Where(x => ids.Contains(x.Id)).ToArray();
        var staged = await files.ValidateStagedFilesAsync(string.Join(",", ids.Except(retained.Select(x => x.Id))),
            user.Id, ObjectId.Empty, "shared-resource");
        if (staged is null) throw new ArgumentException("Tệp không thuộc quyền sở hữu hoặc không phải tệp kho học liệu.");
        var attachments = retained.Concat(staged).ToList();
        if (attachments.Count == 0 && string.IsNullOrWhiteSpace(request.ExternalUrl))
            throw new ArgumentException("Cần ít nhất một tệp hoặc đường dẫn học liệu.");
        return new ResourceVersion { Number = (resource?.Versions.Last().Number ?? 0) + 1,
            Title = request.Title.Trim(), Description = request.Description.Trim(), Category = request.Category.Trim(),
            AuthorName = request.AuthorName.Trim(), Source = request.Source.Trim(), License = request.License.Trim(),
            UsageTerms = request.UsageTerms.Trim(), Access = request.Access, ExternalUrl = request.ExternalUrl.Trim(),
            SubjectIds = requestedSubjects.Select(ObjectId.Parse).ToList(), Files = attachments,
            EditorId = user.Id, EditorName = user.FullName };
    }

    public async Task<LearningResource> CreateAsync(User user, ResourceRequest request)
    {
        var resource = new LearningResource { OwnerId = user.Id };
        resource.Versions.Add(await BuildVersionAsync(user, request, null));
        await db.Resources.InsertOneAsync(resource);
        return resource;
    }

    public async Task<bool> EditAsync(User user, LearningResource resource, ResourceRequest request)
    {
        if (!await CanManageAsync(user, resource)) throw new UnauthorizedAccessException();
        if (resource.IsArchived || resource.Versions.Last().State == ResourceStates.Pending || resource.Versions.Count >= 50)
            throw new ArgumentException("Không thể sửa bản đang chờ duyệt, bản đã lưu trữ hoặc tài liệu đã đạt 50 phiên bản.");
        resource.Versions.Add(await BuildVersionAsync(user, request, resource));
        return await SaveAsync(resource, request.Revision);
    }

    public async Task<bool> TransitionAsync(User user, LearningResource resource, string action, ResourceActionRequest request)
    {
        var version = resource.Versions.Last();
        if (resource.IsArchived) throw new ArgumentException("Tài liệu đã được lưu trữ.");
        if (action == "review")
        {
            if (!await CanReviewAsync(user, version) || resource.OwnerId == user.Id || version.EditorId == user.Id)
                throw new UnauthorizedAccessException();
            if (version.State != ResourceStates.Pending) throw new ArgumentException("Phiên bản không ở trạng thái chờ duyệt.");
            if (!request.Approve && string.IsNullOrWhiteSpace(request.Comment)) throw new ArgumentException("Cần nhận xét khi từ chối.");
            version.State = request.Approve ? ResourceStates.Approved : ResourceStates.Rejected;
            version.ReviewerId = user.Id; version.ReviewerName = user.FullName;
            version.ReviewedAt = DateTime.UtcNow; version.ReviewComment = request.Comment.Trim();
        }
        else
        {
            if (!await CanManageAsync(user, resource)) throw new UnauthorizedAccessException();
            if (action != "archive")
            {
                var subjects = await AuthorSubjectsAsync(user);
                if (version.SubjectIds.Any(id => !subjects.Any(s => s.Id == id)))
                    throw new UnauthorizedAccessException();
            }
            switch (action)
            {
                case "submit" when version.State == ResourceStates.Draft:
                    version.State = ResourceStates.Pending; version.SubmittedAt = DateTime.UtcNow; break;
                case "publish" when version.State == ResourceStates.Approved:
                    version.State = ResourceStates.Published; resource.PublishedVersion = version.Number; break;
                case "archive": resource.IsArchived = true; break;
                default: throw new ArgumentException("Trạng thái không hợp lệ cho thao tác này.");
            }
        }
        var saved = await SaveAsync(resource, request.Revision);
        if (saved)
        {
            var auditSubject = await db.Subjects.Find(x => version.SubjectIds.Contains(x.Id)).FirstOrDefaultAsync();
            await events.RecordAsync($"resource.{action}", user, "LearningResource", resource.Id,
                $"Học liệu đã thực hiện thao tác {action}", departmentId: auditSubject?.DepartmentId);
        }
        if (saved && action == "submit")
        {
            var departments = await db.Subjects.Find(x => version.SubjectIds.Contains(x.Id))
                .Project(x => x.DepartmentId).ToListAsync();
            foreach (var department in departments.Distinct())
                await notifications.NotifyDepartmentAsync(department, "resource.review", "Học liệu chờ kiểm duyệt",
                    version.Title, $"/app/resources/{resource.Id}", user.Id);
        }
        else if (saved && action == "review")
            await notifications.AddAsync(resource.OwnerId, "resource.reviewed",
                request.Approve ? "Học liệu đã được duyệt" : "Học liệu cần chỉnh sửa",
                request.Approve ? version.Title : version.ReviewComment, $"/app/resources/{resource.Id}");
        return saved;
    }

    private async Task<bool> SaveAsync(LearningResource resource, int expectedRevision)
    {
        // A stale browser cannot approve or publish content changed by another request.
        if (resource.Revision != expectedRevision) return false;
        resource.Revision++; resource.UpdatedAt = DateTime.UtcNow;
        var result = await db.Resources.ReplaceOneAsync(x => x.Id == resource.Id && x.Revision == expectedRevision, resource);
        return result.MatchedCount == 1;
    }
}
