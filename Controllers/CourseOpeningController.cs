using System.Security.Claims;
using LMS.Data;
using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Controllers;

[ApiController, Route("api/course-openings"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
public sealed class CourseOpeningController(CourseOpeningService openings, UserService users, MongoContext db) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var user = await CurrentAsync();
        return user is null ? Unauthorized() : Ok(await openings.ListAsync(user));
    }

    [HttpGet("options"), Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Options()
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        var subjects = await db.Subjects.Find(x => user.TeachingSubjectIds.Contains(x.Id) && !x.IsArchived).SortBy(x => x.Name).ToListAsync();
        var terms = await db.AcademicTerms.Find(x => x.EndsAt > DateTime.UtcNow).SortBy(x => x.StartsAt).ToListAsync();
        return Ok(new { subjects = subjects.Select(x => new { id = x.Id.ToString(), x.Code, x.Name }),
            terms = terms.Select(x => new { id = x.Id.ToString(), x.Code, x.Name }) });
    }

    [HttpPost, Authorize(Roles = Roles.Teacher), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CourseOpeningCreateRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try
        {
            var item = await openings.CreateAsync(user, request);
            var response = (await openings.ListAsync(user)).First(x => x.Id == item.Id.ToString());
            return Created($"/api/course-openings/{item.Id}", response);
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
    }

    [HttpPost("{id}/decision"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(string id, CourseOpeningDecisionRequest request)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return NotFound();
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try { await openings.DecideAsync(objectId, user, request); return NoContent(); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (InvalidOperationException error) { return Problem(statusCode: 409, title: error.Message); }
    }
}
