using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[ApiController, Route("api/frontend"), Authorize]
public sealed class FrontendController(
    IAntiforgery antiforgery, AuthService auth, PasswordResetService passwordReset,
    UserService users, ClassService classes, SubjectService subjects) : ControllerBase
{
    [HttpGet("csrf"), AllowAnonymous]
    public IActionResult Csrf()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { requestToken = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });
    }

    [HttpPost("login"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(ApiLoginRequest request)
    {
        var result = await auth.LoginAsync(new LoginViewModel { Email = request.Email, Password = request.Password }, HttpContext);
        if (!result.Success)
            return Problem(statusCode: 401, title: "Email hoặc mật khẩu không đúng.");
        var user = result.User!;
        var permissions = user.PermissionCodes.Where(code => PermissionCodes.IsAllowedForRole(user.Role, code)).ToArray();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role)
        }.Concat(permissions.Select(code => new Claim(PermissionCodes.ClaimType, code)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(new ApiIdentityResponse(user.Id.ToString(), user.FullName, user.Email, user.Role, permissions));
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpPost("forgot-password"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel request, CancellationToken cancellationToken)
    {
        try { await passwordReset.RequestCodeAsync(request.Email, cancellationToken); }
        catch (EmailDeliveryException)
        {
            return Problem(statusCode: 503, title: "Hiện chưa thể gửi email khôi phục mật khẩu. Vui lòng thử lại sau hoặc liên hệ quản trị viên.");
        }
        return Ok(new { message = "Nếu email thuộc tài khoản đang hoạt động, mã OTP sẽ được gửi đến hộp thư đó. Mã có hiệu lực trong 10 phút." });
    }

    [HttpPost("reset-password"), AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel request, CancellationToken cancellationToken)
    {
        if (!await passwordReset.ResetPasswordAsync(request.Email, request.Code, request.NewPassword, cancellationToken))
            return Problem(statusCode: 400, title: "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng.");
        return Ok(new { message = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới." });
    }

    [HttpGet("classes/{id}"), RequirePermission(PermissionCodes.ClassesView)]
    public async Task<IActionResult> ClassSummary(string id)
    {
        var user = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        var classroom = await classes.GetAsync(id);
        if (classroom is null) return NotFound();
        if (user is null || !await classes.HasAccessAsync(id, user)) return Forbid();
        var subject = await subjects.GetAsync(classroom.SubjectId.ToString());
        var teacher = await users.GetAsync(classroom.TeacherId.ToString());
        return Ok(new { id = classroom.Id.ToString(), classroom.Name, classroom.Description,
            subjectName = subject?.Name ?? "Môn học", teacherName = teacher?.FullName ?? "Chưa phân công",
            enrolledCount = classroom.StudentIds.Count, classroom.EnrollmentCapacity });
    }
}
