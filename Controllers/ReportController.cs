using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LMS.Controllers;

[ApiController, Route("api/reports"), Authorize, RequirePermission(PermissionCodes.ReportsView)]
public sealed class ReportController(LearningReportService reports, UserService users) : ControllerBase
{
    [HttpGet("learning")]
    public async Task<IActionResult> Learning([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        var user = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user is null) return Unauthorized();
        if (!TryRange(from, to, out var start, out var end, out var error)) return BadRequest(new ProblemDetails { Title = error });
        try { return Ok(await reports.BuildAsync(user, start, end)); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("learning.csv")]
    public async Task<IActionResult> LearningCsv([FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        var user = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        if (user is null) return Unauthorized();
        if (!TryRange(from, to, out var start, out var end, out var error)) return BadRequest(new ProblemDetails { Title = error });
        try
        {
            var report = await reports.BuildAsync(user, start, end);
            var csv = new StringBuilder("\uFEFFNhóm,Chỉ số,Giá trị,Đơn vị,Thời gian\r\n");
            foreach (var item in report.Metrics) csv.AppendLine($"Chỉ số,{Csv(item.Label)},{item.Value},{Csv(item.Unit)},");
            foreach (var item in report.SupportStatuses) csv.AppendLine($"Hỗ trợ,{Csv(item.Label)},{item.Value},yêu cầu,");
            foreach (var item in report.RecentEvents) csv.AppendLine($"Sự kiện,{Csv(item.Summary)},,,{item.OccurredAt:O}");
            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", $"bao-cao-hoc-tap-{DateTime.UtcNow:yyyyMMdd}.csv");
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    private static bool TryRange(DateOnly? from, DateOnly? to, out DateTime? start, out DateTime? end, out string? error)
    {
        start = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        end = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        error = null;
        if (start.HasValue && end.HasValue && (end <= start || end.Value - start.Value > TimeSpan.FromDays(367)))
        { error = "Khoảng ngày phải hợp lệ và không vượt quá 366 ngày."; return false; }
        return true;
    }

    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
