using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/quizzes"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher + "," + Roles.Student),
 RequirePermission(PermissionCodes.QuizzesView)]
public sealed class QuizController(QuizService quizzes, UserService users) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var user = await CurrentAsync(); return user is null ? Unauthorized() : Ok(await quizzes.ListAsync(user));
    }

    [HttpGet("options"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuizzesManage)]
    public async Task<IActionResult> Options(string? classId = null)
    {
        var user = await CurrentAsync(); return user is null ? Unauthorized() : Ok(await quizzes.EditorOptionsAsync(user, classId));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var user = await CurrentAsync(); var quiz = await quizzes.GetAsync(id);
        if (user is null) return Unauthorized(); if (quiz is null) return NotFound();
        var response = await quizzes.DetailsAsync(user, quiz); return response is null ? NotFound() : Ok(response);
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuizzesManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuizRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try { var quiz = await quizzes.CreateAsync(user, request); return Created($"/api/quizzes/{quiz.Id}", await quizzes.DetailsAsync(user, quiz)); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPut("{id}"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuizzesManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, QuizRequest request) => await MutateAsync(id,
        (user, quiz) => quizzes.EditAsync(user, quiz, request));

    [HttpPost("{id}/publish"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuizzesManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(string id, QuizActionRequest request) => MutateAsync(id,
        (user, quiz) => quizzes.TransitionAsync(user, quiz, "publish", request.Revision));

    [HttpPost("{id}/archive"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuizzesManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(string id, QuizActionRequest request) => MutateAsync(id,
        (user, quiz) => quizzes.TransitionAsync(user, quiz, "archive", request.Revision));

    private async Task<IActionResult> MutateAsync(string id, Func<User, Quiz, Task<bool>> action)
    {
        var user = await CurrentAsync(); var quiz = await quizzes.GetAsync(id);
        if (user is null) return Unauthorized(); if (quiz is null) return NotFound();
        try
        {
            if (!await action(user, quiz)) return Problem(statusCode: 409, title: "Bài kiểm tra đã thay đổi. Vui lòng tải lại.");
            return Ok(await quizzes.DetailsAsync(user, quiz));
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost("{id}/attempts"), Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.QuizzesAttempt), ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(string id)
    {
        var user = await CurrentAsync(); var quiz = await quizzes.GetAsync(id);
        if (user is null) return Unauthorized(); if (quiz is null) return NotFound();
        try { return Ok(await quizzes.StartAsync(user, quiz)); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("attempts/{id}")]
    public async Task<IActionResult> Attempt(string id)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        var response = await quizzes.GetAttemptAsync(user, id); return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("attempts/{id}/submit"), Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.QuizzesAttempt), ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(string id, QuizSubmitRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try { return Ok(await quizzes.SubmitAsync(user, id, request.Answers)); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
