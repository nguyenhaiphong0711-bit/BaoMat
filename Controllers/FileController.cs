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
    private readonly FileStorageService _files;
    private readonly AssignmentService _assignments;
    private readonly SubmissionService _submissions;
    private readonly ClassService _classes;
    private readonly UserService _users;

    public FileController(
        FileStorageService files,
        AssignmentService assignments,
        SubmissionService submissions,
        ClassService classes,
        UserService users)
    {
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
        if (assignment is not null)
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
            if (submission is null || parentAssignment is null ||
                (user.Id == submission.StudentId && !await _users.HasPermissionAsync(user.Id, PermissionCodes.SubmissionsSubmit)) ||
                (user.Id != submission.StudentId && !await _users.HasPermissionAsync(user.Id, PermissionCodes.SubmissionsReview)) ||
                (user.Id != submission.StudentId &&
                 !await _classes.HasAccessAsync(parentAssignment.ClassId.ToString(), user)))
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
