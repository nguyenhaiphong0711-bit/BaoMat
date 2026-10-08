using System.Security.Claims;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using LMS.Security;

namespace LMS.Controllers;

[Authorize]
public class UploadController : Controller
{
    private readonly FileStorageService _files;
    private readonly AssignmentService _assignments;
    private readonly SubmissionService _submissions;
    private readonly ClassService _classes;
    private readonly UserService _users;

    public UploadController(
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

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.AssignmentsManage), ValidateAntiForgeryToken]
    [RequestSizeLimit(FileStorageService.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileStorageService.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> AssignmentAttachment(string classId)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId))
            return BadRequest("Lớp học không hợp lệ.");

        var user = await GetCurrentUserAsync();
        if (user is null || !await _classes.HasAccessAsync(classId, user))
            return Forbid();

        var file = Request.Form.Files.GetFile("file");
        if (file is null)
            return BadRequest("Không tìm thấy tệp tải lên.");

        try
        {
            var reference = await _files.UploadAsync(file, user.Id, classObjectId, "assignment-resource");
            return Content(reference.Id.ToString(), "text/plain");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost, Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.SubmissionsSubmit), ValidateAntiForgeryToken]
    [RequestSizeLimit(FileStorageService.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileStorageService.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> SubmissionFile(string assignmentId)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        var user = await GetCurrentUserAsync();
        if (assignment is null || user is null ||
            !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user) ||
            !assignment.IsPublished || assignment.IsArchived || assignment.DueDate <= DateTime.UtcNow)
        {
            return Forbid();
        }

        var file = Request.Form.Files.GetFile("file");
        if (file is null)
            return BadRequest("Không tìm thấy tệp tải lên.");

        try
        {
            var reference = await _files.UploadAsync(
                file, user.Id, assignment.ClassId, "submission", assignment.Id);
            return Content(reference.Id.ToString(), "text/plain");
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete, ValidateAntiForgeryToken]
    public async Task<IActionResult> Revert()
    {
        using var reader = new StreamReader(Request.Body);
        var rawId = (await reader.ReadToEndAsync()).Trim().Trim('"');
        if (!ObjectId.TryParse(rawId, out var fileId) ||
            !ObjectId.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var ownerId))
        {
            return BadRequest("Mã tệp không hợp lệ.");
        }

        return await _files.DeleteStagedFileAsync(fileId, ownerId)
            ? NoContent()
            : NotFound();
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
}
