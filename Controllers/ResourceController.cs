using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;

namespace LMS.Controllers;

[ApiController, Route("api/resources"), Authorize, RequirePermission(PermissionCodes.ResourcesView)]
public sealed class ResourceController(ResourceService resources, UserService users, FileStorageService files,
    LearningEventService events) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, StringLength(120)] string? q = null,
        [FromQuery, StringLength(80)] string? category = null, string? subjectId = null, string? state = null,
        [FromQuery, Range(1, 100000)] int page = 1)
    {
        var user = await CurrentAsync();
        return user is null ? Unauthorized() : Ok(await resources.ListAsync(user, q, category, subjectId, state, page));
    }

    [HttpGet("subjects"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage)]
    public async Task<IActionResult> AuthorSubjects()
    {
        var user = await CurrentAsync();
        return user is null ? Unauthorized() : Ok((await resources.AuthorSubjectsAsync(user))
            .Select(s => new { id = s.Id.ToString(), s.Name, s.Code }));
    }

    [HttpGet("{id}")]
    [HttpGet("{id}/versions/{version:int}")]
    public async Task<IActionResult> Details(string id, int? version = null)
    {
        var user = await CurrentAsync(); var resource = await resources.GetAsync(id);
        if (user is null) return Unauthorized();
        var visible = resource is null ? null : await resources.VisibleVersionAsync(user, resource, version);
        if (visible is null) return NotFound();
        if (user.Role == Roles.Student)
            await events.RecordDailyAsync("resource.viewed", user, "LearningResource", resource!.Id,
                $"Đã xem học liệu: {visible.Title}", user.Id, user.DepartmentId);
        return Ok(await resources.ToResponseAsync(user, resource!, visible));
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResourceRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try
        {
            var resource = await resources.CreateAsync(user, request);
            return Created($"/api/resources/{resource.Id}", await resources.ToResponseAsync(user, resource, resource.Versions.Last()));
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
    }

    [HttpPut("{id}"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ResourceRequest request) => await MutateAsync(id,
        (user, resource) => resources.EditAsync(user, resource, request));

    [HttpPost("{id}/submit"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(string id, ResourceActionRequest request) => ChangeAsync(id, "submit", request);
    [HttpPost("{id}/publish"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(string id, ResourceActionRequest request) => ChangeAsync(id, "publish", request);
    [HttpPost("{id}/archive"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(string id, ResourceActionRequest request) => ChangeAsync(id, "archive", request);
    [HttpPost("{id}/review"), Authorize(Roles = Roles.Admin + "," + Roles.Staff), RequirePermission(PermissionCodes.ResourcesReview), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(string id, ResourceActionRequest request) => ChangeAsync(id, "review", request);

    private Task<IActionResult> ChangeAsync(string id, string action, ResourceActionRequest request) =>
        MutateAsync(id, (user, resource) => resources.TransitionAsync(user, resource, action, request));

    private async Task<IActionResult> MutateAsync(string id, Func<User, LearningResource, Task<bool>> save)
    {
        var user = await CurrentAsync(); var resource = await resources.GetAsync(id);
        if (user is null) return Unauthorized(); if (resource is null) return NotFound();
        try
        {
            if (!await save(user, resource)) return Problem(statusCode: 409,
                title: "Tài liệu đã thay đổi. Vui lòng tải lại trước khi tiếp tục.");
            return Ok(await resources.ToResponseAsync(user, resource, resource.Versions.Last()));
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("files"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.ResourcesManage), ValidateAntiForgeryToken]
    [RequestSizeLimit(FileStorageService.MaxFileSize + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = FileStorageService.MaxFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try
        {
            var stored = await files.UploadResourceAsync(file, user.Id);
            return Ok(new ApiFileResponse(stored.Id.ToString(), stored.FileName, stored.Length));
        }
        catch (InvalidOperationException error) { return Problem(statusCode: 400, title: error.Message); }
    }

    [HttpGet("{id}/versions/{version:int}/files/{fileId}")]
    public async Task<IActionResult> Download(string id, int version, string fileId)
    {
        var user = await CurrentAsync(); var resource = await resources.GetAsync(id);
        if (user is null) return Unauthorized();
        if (resource is null || !ObjectId.TryParse(fileId, out var parsed)) return NotFound();
        var visible = await resources.VisibleVersionAsync(user, resource, version);
        var file = visible?.Files.FirstOrDefault(f => f.Id == parsed);
        if (file is null) return NotFound();
        try
        {
            var stream = await files.OpenDownloadStreamAsync(parsed);
            if (user.Role == Roles.Student)
                await events.RecordDailyAsync("resource.downloaded", user, "LearningResource", resource.Id,
                    $"Đã tải tệp học liệu: {file.FileName}", user.Id, user.DepartmentId);
            return File(stream, file.ContentType, file.FileName);
        }
        catch (GridFSFileNotFoundException) { return NotFound(); }
    }
}
