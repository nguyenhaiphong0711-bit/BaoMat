using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using MongoDB.Bson;
using LMS.Security;
namespace LMS.Controllers;
[Authorize] public class AssignmentController : Controller
{
    private readonly AssignmentService _assignments; private readonly ClassService _classes; private readonly UserService _users; private readonly FileStorageService _files; private readonly SubmissionService _submissions;
    public AssignmentController(AssignmentService a,ClassService c,UserService u,FileStorageService files,SubmissionService submissions){_assignments=a;_classes=c;_users=u;_files=files;_submissions=submissions;}
    [RequirePermission(PermissionCodes.AssignmentsView)]
    public async Task<IActionResult> Index(string classId, CatalogFilterViewModel filter)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId)) return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null || !await _classes.HasAccessAsync(classId, user)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        var assignments = await _assignments.GetByClassAsync(classObjectId, user.Role == Roles.Student);
        if (user.Role == Roles.Student)
        {
            var ownSubmissions = await _submissions.GetMineForClassAsync(classObjectId, user.Id);
            ViewBag.SubmissionsByAssignment = ownSubmissions.ToDictionary(x => x.AssignmentId.ToString());
        }
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            assignments = assignments.Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && bool.TryParse(filter.Status, out var published))
            assignments = assignments.Where(x => x.IsPublished == published).ToList();
        if (filter.CreatedFrom.HasValue)
            assignments = assignments.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            assignments = assignments.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        if (filter.StartFrom.HasValue)
            assignments = assignments.Where(x => x.DueDate.Date >= filter.StartFrom.Value.Date).ToList();
        if (filter.StartTo.HasValue)
            assignments = assignments.Where(x => x.DueDate.Date <= filter.StartTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        var page = PaginationViewModel.Apply(assignments, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.AssignmentsManage)]
    public async Task<IActionResult> Create(string classId)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        return View(new AssignmentViewModel());
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),RequirePermission(PermissionCodes.AssignmentsManage),ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string classId, AssignmentViewModel vm)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        if (!ModelState.IsValid)
        {
            await SetClassViewDataAsync(classId);
            return View(vm);
        }
        var user = await GetCurrentUserAsync();
        var attachments = user is null ? null : await _files.ValidateStagedFilesAsync(
            vm.UploadedFileIds, user.Id, ObjectId.Parse(classId), "assignment-resource");
        if (attachments is null)
        {
            ModelState.AddModelError(nameof(vm.UploadedFileIds), "Tệp tải lên không hợp lệ hoặc không được phép.");
            await SetClassViewDataAsync(classId);
            return View(vm);
        }
        await _assignments.CreateAsync(ObjectId.Parse(classId), vm, attachments);
        return RedirectToAction(nameof(Index), new { classId });
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.AssignmentsManage)]
    public async Task<IActionResult> Edit(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        ViewBag.ClassId = assignment.ClassId.ToString();
        ViewBag.ClassName = (await _classes.GetAsync(assignment.ClassId.ToString()))?.Name ?? "";
        ViewBag.AssignmentAttachments = assignment.Attachments;
        return View(new AssignmentViewModel
        {
            Title = assignment.Title,
            Description = assignment.Description,
            DueDate = assignment.DueDate,
            IsPublished = assignment.IsPublished,
            UploadedFileIds = string.Join(',', assignment.Attachments.Select(x => x.Id))
        });
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),RequirePermission(PermissionCodes.AssignmentsManage),ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, AssignmentViewModel vm)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        if (!ModelState.IsValid)
        {
            await SetClassViewDataAsync(assignment.ClassId.ToString());
            ViewBag.AssignmentAttachments = assignment.Attachments;
            return View(vm);
        }
        var requestedIds = FileStorageService.ParseIds(vm.UploadedFileIds);
        var user = await GetCurrentUserAsync();
        if (requestedIds is null || user is null || requestedIds.Count > FileStorageService.MaxFilesPerUpload)
        {
            ModelState.AddModelError(nameof(vm.UploadedFileIds), "Danh sách tệp không hợp lệ.");
            await SetClassViewDataAsync(assignment.ClassId.ToString());
            ViewBag.AssignmentAttachments = assignment.Attachments;
            return View(vm);
        }

        var existingById = assignment.Attachments.ToDictionary(x => x.Id);
        var retained = requestedIds.Where(existingById.ContainsKey).Select(fileId => existingById[fileId]).ToList();
        var newIds = requestedIds.Where(fileId => !existingById.ContainsKey(fileId)).ToArray();
        var newAttachments = await _files.ValidateStagedFilesAsync(
            string.Join(',', newIds), user.Id, assignment.ClassId, "assignment-resource");
        if (newAttachments is null)
        {
            ModelState.AddModelError(nameof(vm.UploadedFileIds), "Tệp tải lên không hợp lệ hoặc không được phép.");
            await SetClassViewDataAsync(assignment.ClassId.ToString());
            ViewBag.AssignmentAttachments = assignment.Attachments;
            return View(vm);
        }

        var oldIds = assignment.Attachments.Select(x => x.Id).ToHashSet();
        var savedIds = requestedIds.ToHashSet();
        await _assignments.UpdateAsync(id, vm, retained.Concat(newAttachments).ToList());
        foreach (var removedId in oldIds.Except(savedIds))
            await _files.DeleteIfUnreferencedAsync(removedId);
        return RedirectToAction(nameof(Index), new { classId = assignment.ClassId });
    }

    [HttpPost, Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.AssignmentsManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        await _assignments.ArchiveAsync(id);
        TempData["Success"] = "Bài tập đã được lưu trữ; các bài nộp và điểm vẫn được giữ.";
        return RedirectToAction(nameof(Index), new { classId = assignment.ClassId });
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");

    private async Task SetClassViewDataAsync(string classId)
    {
        ViewBag.ClassId = classId;
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
    }

    private async Task<bool> CanManageClassAsync(string classId)
    {
        var user = await GetCurrentUserAsync();
        return user is not null && await _classes.HasAccessAsync(classId, user);
    }
}
