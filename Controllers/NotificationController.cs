using System.Security.Claims;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace LMS.Controllers;

[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationController(NotificationService notifications, UserService users) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        var rows = await notifications.ListAsync(user.Id);
        return Ok(new { unread = await notifications.UnreadAsync(user.Id), items = rows.Select(x =>
            new NotificationResponse(x.Id.ToString(), x.Type, x.Title, x.Message, x.Link, x.CreatedAt, x.ReadAt)) });
    }

    [HttpPost("read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll()
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        await notifications.MarkReadAsync(user.Id); return NoContent();
    }

    [HttpPost("{id}/read"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(string id)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        if (!ObjectId.TryParse(id, out var objectId)) return NotFound();
        await notifications.MarkReadAsync(user.Id, objectId); return NoContent();
    }
}
