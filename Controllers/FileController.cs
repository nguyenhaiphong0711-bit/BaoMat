using System.Security.Claims;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;
using LMS.Security;

namespace LMS.Controllers;

[Authorize]
public class FileController : Controller
{
    private readonly LessonService _lessons;
    private readonly FileStorageService _files;
    private readonly AssignmentService _assignments;
    private readonly SubmissionService _submissions;
    private readonly ClassService _classes;
    private readonly UserService _users;

    public FileController(
        LessonService lessons,
        FileStorageService files,
        AssignmentService assignments,
        SubmissionService submissions,
        ClassService classes,
        UserService users)
    {
        _lessons = lessons;
        _files = files;
        _assignments = assignments;
        _submissions = submissions;
        _classes = classes;
        _users = users;
    }

    public async Task<IActionResult> Download(string id)
    {
        if (!ObjectId.TryParse(id, out var fileId))
            return NotFound();

        var info = await _files.GetInfoAsync(fileId);
        var reference = await _files.GetReferenceAsync(fileId);
        var user = await _users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (info is null || reference is null || user is null)
            return NotFound();

        var assignment = await _assignments.GetByAttachmentAsync(fileId);
        var lesson = await _lessons.GetByAttachmentAsync(fileId);
        if (lesson is not null)
        {
            if (!await _users.HasPermissionAsync(user.Id, PermissionCodes.LessonsView) ||
                !await _classes.HasAccessAsync(lesson.ClassId.ToString(), user) ||
                (user.Role == Roles.Student && (!lesson.IsPublished || lesson.IsArchived)))
                return Forbid();
        }
        else if (assignment is not null)
        {
            var requiredPermission = assignment.Attachments.Any(file => file.Id == fileId)
                ? PermissionCodes.AssignmentsView
                : PermissionCodes.SubmissionsReview;
            if (!await _users.HasPermissionAsync(user.Id, requiredPermission) ||
                !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user) ||
                (user.Role == Roles.Student && (!assignment.IsPublished || assignment.IsArchived)))
            {
                return Forbid();
            }
        }
        else
        {
            var submission = await _submissions.GetByAttachmentAsync(fileId);
            var parentAssignment = submission is null
                ? null
                : await _assignments.GetAsync(submission.AssignmentId.ToString());
            var ownsSubmission = user.Role == Roles.Student && user.Id == submission?.StudentId;
            var canReview = user.Role is Roles.Admin or Roles.Teacher;
            if (submission is null || parentAssignment is null ||
                !await _classes.HasAccessAsync(parentAssignment.ClassId.ToString(), user) ||
                (!ownsSubmission && !canReview) ||
                (ownsSubmission && !await _users.HasPermissionAsync(user.Id, PermissionCodes.SubmissionsSubmit)) ||
                (!ownsSubmission && !await _users.HasPermissionAsync(user.Id, PermissionCodes.SubmissionsReview)))
            {
                return Forbid();
            }
        }

        try
        {
            var stream = await _files.OpenDownloadStreamAsync(fileId);
            return File(stream, reference.ContentType, reference.FileName, enableRangeProcessing: true);
        }
        catch (GridFSFileNotFoundException)
        {
            return NotFound();
        }
    }
}
