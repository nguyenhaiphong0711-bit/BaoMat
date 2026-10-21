using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/reports"), Authorize, RequirePermission(PermissionCodes.ReportsView)]
public sealed class ReportController(LearningReportService reports, UserService users) : ControllerBase
{
    [HttpGet("learning")]
    public async Task<IActionResult> Learning()
    {
        var user = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user is null) return Unauthorized();
        try { return Ok(await reports.BuildAsync(user)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
