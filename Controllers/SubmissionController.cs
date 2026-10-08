using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using MongoDB.Bson;
using LMS.Security;
namespace LMS.Controllers;
[Authorize] public class SubmissionController : Controller
{
    private readonly SubmissionService _subs; private readonly AssignmentService _assignments; private readonly ClassService _classes; private readonly UserService _users; private readonly FileStorageService _files;
    public SubmissionController(SubmissionService s,AssignmentService a,ClassService c,UserService u,FileStorageService files){_subs=s;_assignments=a;_classes=c;_users=u;_files=files;}
    [Authorize(Roles=Roles.Student), RequirePermission(PermissionCodes.SubmissionsSubmit)]
    public async Task<IActionResult> Submit(string assignmentId)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        var user = await GetCurrentUserAsync();
        if (assignment is null || user is null ||
            !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user) ||
            !assignment.IsPublished || assignment.IsArchived)
            return Forbid();

        var submission = await _subs.GetMineAsync(assignment.Id, user.Id);
        ViewBag.Assignment = assignment;
        ViewBag.Submission = submission;
        return View(new SubmissionViewModel
        {
            Content = submission?.Content ?? "",
            UploadedFileIds = string.Join(",", submission?.Attachments.Select(x => x.Id) ?? Enumerable.Empty<MongoDB.Bson.ObjectId>())
        });
    }

    [HttpPost, Authorize(Roles=Roles.Student), RequirePermission(PermissionCodes.SubmissionsSubmit), ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string assignmentId, SubmissionViewModel vm)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        var user = await GetCurrentUserAsync();
        if (assignment is null || user is not { } student ||
            !await _classes.HasAccessAsync(assignment.ClassId.ToString(), student) ||
            !assignment.IsPublished || assignment.IsArchived)
            return Forbid();

        if (assignment.DueDate <= DateTime.UtcNow)
        {
            TempData["Error"] = "Hạn nộp bài đã qua.";
            return RedirectToAction("Index", "Assignment", new { classId = assignment.ClassId });
        }

        var existing = await _subs.GetMineAsync(assignment.Id, student.Id);
        var requestedIds = FileStorageService.ParseIds(vm.UploadedFileIds);
        if (requestedIds is null || requestedIds.Count > FileStorageService.MaxFilesPerUpload)
        {
            ModelState.AddModelError(nameof(vm.UploadedFileIds), "Danh sách tệp không hợp lệ.");
        }
        else if (string.IsNullOrWhiteSpace(vm.Content) && requestedIds.Count == 0)
        {
            ModelState.AddModelError(nameof(vm.Content), "Nhập câu trả lời hoặc tải lên ít nhất một tệp.");
        }

        IReadOnlyList<StoredFileReference>? attachments = null;
        if (requestedIds is not null)
        {
            var existingById = existing?.Attachments.ToDictionary(x => x.Id) ?? new Dictionary<MongoDB.Bson.ObjectId, StoredFileReference>();
            var retained = requestedIds.Where(existingById.ContainsKey).Select(fileId => existingById[fileId]).ToList();
            var newIds = requestedIds.Where(fileId => !existingById.ContainsKey(fileId)).ToArray();
            var newAttachments = await _files.ValidateStagedFilesAsync(
                string.Join(",", newIds), student.Id, assignment.ClassId, "submission", assignment.Id);
            if (newAttachments is null)
                ModelState.AddModelError(nameof(vm.UploadedFileIds), "Tệp tải lên không hợp lệ hoặc không được phép.");
            else
                attachments = retained.Concat(newAttachments).ToList();
        }

        if (!ModelState.IsValid || attachments is null)
        {
            ViewBag.Assignment = assignment;
            ViewBag.Submission = existing;
            return View(vm);
        }

        var previousIds = existing?.Attachments.Select(x => x.Id).ToHashSet() ?? new HashSet<MongoDB.Bson.ObjectId>();
        var savedIds = attachments.Select(x => x.Id).ToHashSet();
        await _subs.SubmitAsync(assignment.Id, student.Id, vm, attachments);
        foreach (var removedId in previousIds.Except(savedIds))
            await _files.DeleteIfUnreferencedAsync(removedId);
        return RedirectToAction("Index", "Assignment", new { classId = assignment.ClassId });
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.SubmissionsReview)]
    public async Task<IActionResult> Index(string assignmentId, CatalogFilterViewModel filter)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        var user = await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        if (assignment is null || user is null || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user)) return Forbid();

        var submissions = await _subs.GetByAssignmentAsync(assignment.Id);
        var students = await _users.GetByIdsAsync(submissions.Select(x => x.StudentId).Distinct().ToArray());
        var studentsById = students.ToDictionary(x => x.Id);
        ViewBag.Assignment = assignment;
        ViewBag.ClassName = (await _classes.GetAsync(assignment.ClassId.ToString()))?.Name ?? "";
        var rows = submissions.Select(submission => new SubmissionListItemViewModel
        {
            Id = submission.Id.ToString(),
            StudentName = studentsById.GetValueOrDefault(submission.StudentId)?.FullName ?? "Unknown student",
            StudentEmail = studentsById.GetValueOrDefault(submission.StudentId)?.Email ?? "",
            Content = submission.Content,
            Attachments = submission.Attachments,
            Grade = submission.Grade,
            TeacherComment = submission.TeacherComment,
            SubmittedAt = new DateTimeOffset(DateTime.SpecifyKind(submission.SubmittedAt, DateTimeKind.Utc))
        }).ToList();
        var page = PaginationViewModel.Apply(rows, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.SubmissionsReview)]
    public async Task<IActionResult> Grade(string id)
    {
        var submission = await _subs.GetAsync(id);
        if (submission is null) return NotFound();
        var assignment = await _assignments.GetAsync(submission.AssignmentId.ToString());
        var user = await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        if (assignment is null || user is null || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user)) return Forbid();
        ViewBag.Submission = submission;
        ViewBag.Student = await _users.GetAsync(submission.StudentId.ToString());
        ViewBag.Assignment = assignment;
        return View(new GradeViewModel { Grade = submission.Grade ?? 0, TeacherComment = submission.TeacherComment });
    }
    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),RequirePermission(PermissionCodes.SubmissionsReview),ValidateAntiForgeryToken]
    public async Task<IActionResult> Grade(string id, GradeViewModel vm)
    {
        var submission = await _subs.GetAsync(id);
        if (submission is null) return NotFound();
        var assignment = await _assignments.GetAsync(submission.AssignmentId.ToString());
        var user = await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        if (assignment is null || user is null || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user)) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.Submission = submission;
            ViewBag.Student = await _users.GetAsync(submission.StudentId.ToString());
            ViewBag.Assignment = assignment;
            return View(vm);
        }
        await _subs.GradeAsync(id, vm);
        return RedirectToAction(nameof(Index), new { assignmentId = assignment.Id });
    }
}
