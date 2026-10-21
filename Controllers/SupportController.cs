using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/support"), Authorize(Roles = Roles.Admin + "," + Roles.Student + "," + Roles.Staff),
 RequirePermission(PermissionCodes.SupportView)]
public sealed class SupportController(SupportService support, UserService users) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, StringLength(120)] string? q = null, string? status = null,
        [FromQuery, Range(1, 100000)] int page = 1)
    {
        var user = await CurrentAsync(); return user is null ? Unauthorized() : Ok(await support.ListAsync(user, q, status, page));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var user = await CurrentAsync(); var ticket = await support.GetAsync(id);
        if (user is null) return Unauthorized(); if (ticket is null) return NotFound();
        var response = await support.ResponseAsync(user, ticket); return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{id}/staff"), Authorize(Roles = Roles.Admin + "," + Roles.Staff), RequirePermission(PermissionCodes.SupportManage)]
    public async Task<IActionResult> Staff(string id)
    {
        var user = await CurrentAsync(); var ticket = await support.GetAsync(id);
        if (user is null) return Unauthorized(); if (ticket is null) return NotFound();
        try { return Ok(await support.EligibleStaffAsync(user, ticket)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost, Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.SupportCreate), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SupportCreateRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try { var ticket = await support.CreateAsync(user, request); return Created($"/api/support/{ticket.Id}", await support.ResponseAsync(user, ticket)); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
    }

    [HttpPost("{id}/messages"), ValidateAntiForgeryToken]
    public Task<IActionResult> Message(string id, SupportMessageRequest request) => MutateAsync(id,
        (user, ticket) => support.AddMessageAsync(user, ticket, request));

    [HttpPut("{id}"), Authorize(Roles = Roles.Admin + "," + Roles.Staff), RequirePermission(PermissionCodes.SupportManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Manage(string id, SupportManageRequest request) => MutateAsync(id,
        (user, ticket) => support.ManageAsync(user, ticket, request));

    private async Task<IActionResult> MutateAsync(string id, Func<User, SupportTicket, Task<bool>> save)
    {
        var user = await CurrentAsync(); var ticket = await support.GetAsync(id);
        if (user is null) return Unauthorized(); if (ticket is null) return NotFound();
        try
        {
            if (!await save(user, ticket)) return Problem(statusCode: 409, title: "Yêu cầu đã thay đổi. Vui lòng tải lại.");
            return Ok(await support.ResponseAsync(user, ticket));
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
