using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/staff"), Authorize(Roles = Roles.Staff)]
public sealed class StaffController(UserService users, StaffAcademicService academics) : ControllerBase
{
    [HttpGet("students"), RequirePermission(PermissionCodes.StaffStudentsView)]
    public async Task<IActionResult> Students([FromQuery, StringLength(120)] string? q = null,
        [FromQuery, Range(1, 100000)] int page = 1)
    {
        var scope = await ScopeAsync();
        return scope is null ? MissingScope() : Ok(await academics.GetStudentsAsync(scope, q, page));
    }

    [HttpGet("students/{id}/progress"), RequirePermission(PermissionCodes.StaffProgressView)]
    public async Task<IActionResult> Progress(string id)
    {
        var scope = await ScopeAsync();
        if (scope is null) return MissingScope();
        var progress = await academics.GetProgressAsync(scope, id);
        // Use the same result for nonexistent and out-of-scope students.
        return progress is null ? NotFound() : Ok(progress);
    }

    private async Task<Department?> ScopeAsync()
    {
        var staff = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        return staff is null ? null : await academics.GetScopeAsync(staff);
    }
    private IActionResult MissingScope() => Problem(statusCode: 403,
        title: "Chưa có phạm vi quản lý hợp lệ. Vui lòng liên hệ Admin để được phân công khoa.");
}
