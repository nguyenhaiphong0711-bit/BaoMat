using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/questions"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher + "," + Roles.Staff),
 RequirePermission(PermissionCodes.QuestionsView)]
public sealed class QuestionBankController(QuestionBankService questions, UserService users) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet]
    public async Task<IActionResult> List([FromQuery, StringLength(120)] string? q = null, string? subjectId = null,
        string? state = null, [FromQuery, Range(1, 100000)] int page = 1)
    {
        var user = await CurrentAsync();
        return user is null ? Unauthorized() : Ok(await questions.ListAsync(user, q, subjectId, state, page));
    }

    [HttpGet("subjects"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage)]
    public async Task<IActionResult> Subjects()
    {
        var user = await CurrentAsync();
        return user is null ? Unauthorized() : Ok((await questions.AuthorSubjectsAsync(user)).Select(x => new { id = x.Id.ToString(), x.Code, x.Name }));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(string id)
    {
        var user = await CurrentAsync(); var question = await questions.GetAsync(id);
        if (user is null) return Unauthorized();
        var response = question is null ? null : await questions.ResponseAsync(user, question);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost, Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(QuestionRequest request)
    {
        var user = await CurrentAsync(); if (user is null) return Unauthorized();
        try { var question = await questions.CreateAsync(user, request); return Created($"/api/questions/{question.Id}", await questions.ResponseAsync(user, question)); }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
    }

    [HttpPut("{id}"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Edit(string id, QuestionRequest request) => MutateAsync(id, (user, question) => questions.EditAsync(user, question, request));

    [HttpPost("{id}/submit"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Submit(string id, QuestionActionRequest request) => ChangeAsync(id, "submit", request);
    [HttpPost("{id}/publish"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(string id, QuestionActionRequest request) => ChangeAsync(id, "publish", request);
    [HttpPost("{id}/archive"), Authorize(Roles = Roles.Admin + "," + Roles.Teacher), RequirePermission(PermissionCodes.QuestionsManage), ValidateAntiForgeryToken]
    public Task<IActionResult> Archive(string id, QuestionActionRequest request) => ChangeAsync(id, "archive", request);
    [HttpPost("{id}/review"), Authorize(Roles = Roles.Admin + "," + Roles.Staff), RequirePermission(PermissionCodes.QuestionsReview), ValidateAntiForgeryToken]
    public Task<IActionResult> Review(string id, QuestionActionRequest request) => ChangeAsync(id, "review", request);

    private Task<IActionResult> ChangeAsync(string id, string action, QuestionActionRequest request) =>
        MutateAsync(id, (user, question) => questions.TransitionAsync(user, question, action, request));

    private async Task<IActionResult> MutateAsync(string id, Func<User, QuestionBankItem, Task<bool>> save)
    {
        var user = await CurrentAsync(); var question = await questions.GetAsync(id);
        if (user is null) return Unauthorized(); if (question is null) return NotFound();
        try
        {
            if (!await save(user, question)) return Problem(statusCode: 409, title: "Câu hỏi đã thay đổi. Vui lòng tải lại.");
            return Ok(await questions.ResponseAsync(user, question));
        }
        catch (ArgumentException error) { return Problem(statusCode: 400, title: error.Message); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
