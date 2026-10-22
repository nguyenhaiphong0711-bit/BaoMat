using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace LMS.Controllers;

[ApiController, Route("api/portal"), Authorize]
public sealed class PortalController(UserService users, AcademicService academics,
    StudentAcademicService studentAcademics, ActivityLogService logs,
    PermissionService permissions, DepartmentService departments, SubjectService subjects,
    VnpayPaymentService vnpay, Microsoft.Extensions.Options.IOptions<VnpaySettings> vnpayOptions,
    IHostEnvironment environment) : ControllerBase
{
    private Task<User?> CurrentAsync() => users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    [HttpGet("student/dashboard"), Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.StudentProgressView)]
    public async Task<IActionResult> StudentDashboard()
    {
        var user = await CurrentAsync();
        return user is { Role: Roles.Student } ? Ok(await studentAcademics.GetDashboardAsync(user)) : Forbid();
    }

    [HttpGet("student/registrations"), Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.CourseRegistrationView)]
    public async Task<IActionResult> Registrations()
    {
        var student = await CurrentAsync(); if (student is not { Role: Roles.Student }) return Forbid();
        var available = await academics.GetEligibleClassesAsync(student);
        var statuses = await academics.GetRegistrationStatusesAsync(student.Id, available.Select(x => x.Class.Id).ToArray());
        var rows = new List<CourseRegistrationItemViewModel>();
        foreach (var (classroom, subject, term) in available)
        {
            var teacher = await users.GetAsync(classroom.TeacherId.ToString());
            statuses.TryGetValue(classroom.Id, out var status);
            rows.Add(new CourseRegistrationItemViewModel { ClassId = classroom.Id.ToString(), ClassName = classroom.Name,
                SubjectCode = subject.Code, SubjectName = subject.Name, TeacherName = teacher?.FullName ?? "Chưa phân công",
                TermName = term.Name, Enrolled = classroom.StudentIds.Count, Capacity = classroom.EnrollmentCapacity,
                IsRegistered = classroom.StudentIds.Contains(student.Id) || status == StudentRegistrationStatuses.Active,
                IsPending = status == StudentRegistrationStatuses.Pending, RegistrationStatus = status ?? "" });
        }
        return Ok(rows);
    }

    [HttpPost("student/registrations/{classId}"), Authorize(Roles = Roles.Student),
     RequirePermission(PermissionCodes.CourseRegistrationManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string classId)
    {
        var student = await CurrentAsync(); if (student is not { Role: Roles.Student }) return Forbid();
        var result = await academics.RegisterAsync(student, classId);
        return result.Success ? Ok(new { result.Message }) : Problem(statusCode: 400, title: result.Message);
    }

    [HttpDelete("student/registrations/{classId}"), Authorize(Roles = Roles.Student),
     RequirePermission(PermissionCodes.CourseRegistrationManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(string classId)
    {
        var student = await CurrentAsync(); if (student is not { Role: Roles.Student }) return Forbid();
        var result = await academics.WithdrawAsync(student, classId);
        return result.Success ? NoContent() : Problem(statusCode: 400, title: result.Message);
    }

    [HttpPost("student/tuition/{classId}/payment"), Authorize(Roles = Roles.Student),
     RequirePermission(PermissionCodes.TuitionPay), ValidateAntiForgeryToken]
    public async Task<IActionResult> Payment(string classId, CancellationToken cancellationToken)
    {
        var student = await CurrentAsync(); if (student is not { Role: Roles.Student }) return Forbid();
        var returnUrl = string.IsNullOrWhiteSpace(vnpayOptions.Value.ReturnUrl)
            ? Url.Action(nameof(VnpayController.Return), "Vnpay", null, Request.Scheme)! : vnpayOptions.Value.ReturnUrl;
        var ipnUrl = string.IsNullOrWhiteSpace(vnpayOptions.Value.IpnUrl)
            ? Url.Action(nameof(VnpayController.Ipn), "Vnpay", null, Request.Scheme) : vnpayOptions.Value.IpnUrl;
        var result = await studentAcademics.CreatePaymentUrlAsync(student, classId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1", returnUrl, ipnUrl,
            vnpay, environment.IsProduction(), cancellationToken);
        return result.Success && result.PaymentUrl is not null ? Ok(new { paymentUrl = result.PaymentUrl })
            : Problem(statusCode: 400, title: result.Message);
    }

    [HttpGet("admin/registration-reviews"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> RegistrationReviews() => Ok(await academics.GetPendingRegistrationReviewsAsync());

    [HttpPost("admin/registration-reviews/{id}"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string id, [FromBody] ReviewRequest request)
    {
        var result = await academics.ReviewRegistrationAsync(id, request.Approve);
        return result.Success ? Ok(new { result.Message }) : Problem(statusCode: 400, title: result.Message);
    }

    [HttpGet("admin/terms"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Terms() => Ok(await academics.GetTermsAsync());

    [HttpPost("admin/terms"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTerm(AcademicTermViewModel request)
    {
        var saved = await academics.SaveTermAsync(new AcademicTerm { Id = ObjectId.TryParse(request.Id, out var id) ? id : ObjectId.Empty,
            Code = request.Code.Trim(), Name = request.Name.Trim(), AcademicYear = request.AcademicYear.Trim(),
            SemesterNumber = request.SemesterNumber, StartsAt = request.StartsAt, EndsAt = request.EndsAt,
            RegistrationOpensAt = request.RegistrationOpensAt, RegistrationClosesAt = request.RegistrationClosesAt,
            TuitionPerCredit = request.TuitionPerCredit, IsActive = request.IsActive });
        return saved ? Ok() : Problem(statusCode: 400, title: "Thông tin học kỳ không hợp lệ hoặc mã bị trùng.");
    }

    [HttpGet("admin/programs"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Programs() => Ok(await academics.GetAllProgramsAsync());

    [HttpPost("admin/programs"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProgram(AcademicProgramViewModel request)
    {
        if (!ObjectId.TryParse(request.DepartmentId, out var departmentId)) return BadRequest();
        var saved = await academics.SaveProgramAsync(new AcademicProgram { Id = ObjectId.TryParse(request.Id, out var id) ? id : ObjectId.Empty,
            DepartmentId = departmentId, Code = request.Code.Trim(), Name = request.Name.Trim(),
            Description = request.Description.Trim() }, request.Curriculum);
        return saved ? Ok() : Problem(statusCode: 400, title: "Ngành học, khoa hoặc chương trình môn học không hợp lệ.");
    }

    [HttpPost("admin/programs/{id}/active"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> ProgramActive(string id, [FromBody] ActiveRequest request)
    {
        var result = await academics.SetProgramActiveAsync(id, request.IsActive);
        return result.Success ? Ok() : Problem(statusCode: 400, title: result.Message);
    }

    [HttpGet("admin/permissions"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Permissions() => Ok(new {
        users = (await users.GetAllAsync()).Where(x => x.Role is Roles.Teacher or Roles.Student or Roles.Staff).Select(x => new {
            id=x.Id.ToString(), x.FullName, x.Email, x.Role, permissionCodes=x.PermissionCodes,
            departmentId=x.DepartmentId?.ToString(), programId=x.ProgramId?.ToString(), x.CurrentSemester,
            teachingSubjectIds=x.TeachingSubjectIds.Select(y=>y.ToString()) }),
        permissions = await permissions.GetAllAsync(), departments = await departments.GetAllAsync(),
        programs = await academics.GetProgramsAsync(), subjects = await subjects.GetAllAsync()
    });

    [HttpPut("admin/permissions/{id}"), Authorize(Roles = Roles.Admin), ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignPermissions(string id, UserPermissionViewModel request)
    {
        request.UserId = id;
        if (!await permissions.AreValidForUserAsync(id, request.PermissionCodes) ||
            !await users.UpdateAcademicProfileAsync(request) || !await permissions.SetForUserAsync(id, request.PermissionCodes))
            return Problem(statusCode: 400, title: "Quyền hoặc hồ sơ học vụ không hợp lệ.");
        return NoContent();
    }

    [HttpGet("admin/activity-logs"), Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ActivityLogs() => Ok((await logs.GetAllAsync()).Take(200));
}

public sealed record ReviewRequest(bool Approve);
public sealed record ActiveRequest(bool IsActive);
