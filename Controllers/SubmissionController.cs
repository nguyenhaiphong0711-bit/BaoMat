using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using MongoDB.Bson;
namespace LMS.Controllers;
[Authorize] public class SubmissionController : Controller
{
    private readonly SubmissionService _subs; private readonly AssignmentService _assignments; private readonly ClassService _classes; private readonly UserService _users;
    public SubmissionController(SubmissionService s,AssignmentService a,ClassService c,UserService u){_subs=s;_assignments=a;_classes=c;_users=u;}
    [Authorize(Roles=Roles.Student)] public async Task<IActionResult> Submit(string assignmentId){var a=await _assignments.GetAsync(assignmentId);var u=await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"");if(a==null||u==null||!await _classes.HasAccessAsync(a.ClassId.ToString(),u)||!a.IsPublished||a.IsArchived)return Forbid();ViewBag.Assignment=a;ViewBag.Submission=await _subs.GetMineAsync(a.Id,u.Id);return View(new SubmissionViewModel{Content=ViewBag.Submission?.Content??""});}
    [HttpPost,Authorize(Roles=Roles.Student),ValidateAntiForgeryToken] public async Task<IActionResult> Submit(string assignmentId,SubmissionViewModel vm){var a=await _assignments.GetAsync(assignmentId);var u=await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value??"");if(a==null||u==null||!await _classes.HasAccessAsync(a.ClassId.ToString(),u)||!a.IsPublished||a.IsArchived)return Forbid();if(a.DueDate<=DateTime.UtcNow){TempData["Error"]="Hạn nộp bài đã qua.";return RedirectToAction("Index","Assignment",new{classId=a.ClassId});}if(!ModelState.IsValid){ViewBag.Assignment=a;return View(vm);}await _subs.SubmitAsync(a.Id,u.Id,vm);return RedirectToAction("Index","Assignment",new{classId=a.ClassId});}
    [Authorize(Roles=Roles.Admin+","+Roles.Teacher)]
    public async Task<IActionResult> Index(string assignmentId)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        var user = await _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        if (assignment is null || user is null || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user)) return Forbid();

        var submissions = await _subs.GetByAssignmentAsync(assignment.Id);
        var students = await _users.GetByIdsAsync(submissions.Select(x => x.StudentId).Distinct().ToArray());
        var studentsById = students.ToDictionary(x => x.Id);
        ViewBag.Assignment = assignment;
        ViewBag.ClassName = (await _classes.GetAsync(assignment.ClassId.ToString()))?.Name ?? "";
        return View(submissions.Select(submission => new SubmissionListItemViewModel
        {
            Id = submission.Id.ToString(),
            StudentName = studentsById.GetValueOrDefault(submission.StudentId)?.FullName ?? "Unknown student",
            StudentEmail = studentsById.GetValueOrDefault(submission.StudentId)?.Email ?? "",
            Content = submission.Content,
            Grade = submission.Grade,
            TeacherComment = submission.TeacherComment,
            SubmittedAt = new DateTimeOffset(DateTime.SpecifyKind(submission.SubmittedAt, DateTimeKind.Utc))
        }).ToList());
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher)]
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
    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),ValidateAntiForgeryToken]
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
