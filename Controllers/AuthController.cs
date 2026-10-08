using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LMS.Controllers;
public class AuthController : Controller
{
    private readonly AuthService _auth;
    private readonly PasswordResetService _passwordReset;
    public AuthController(AuthService auth, PasswordResetService passwordReset)
    {
        _auth = auth;
        _passwordReset = passwordReset;
    }

    [AllowAnonymous] public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _auth.LoginAsync(vm, HttpContext);
        if (!result.Success) { ModelState.AddModelError("", result.Message); return View(vm); }
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User!.Id.ToString()),
            new(ClaimTypes.Name, result.User.FullName),
            new(ClaimTypes.Email, result.User.Email),
            new(ClaimTypes.Role, result.User.Role)
        };
        claims.AddRange(result.User.PermissionCodes.Select(code => new Claim(PermissionCodes.ClaimType, code)));
        var authenticationProperties = new AuthenticationProperties
        {
            IsPersistent = vm.RememberMe
        };
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            authenticationProperties);
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        await _passwordReset.RequestCodeAsync(model.Email, cancellationToken);
        ViewBag.Message = "Nếu email thuộc một tài khoản đang hoạt động, mã OTP sẽ được gửi đến hộp thư đó. Mã có hiệu lực trong 10 phút.";
        return View("ResetPassword", new ResetPasswordViewModel { Email = model.Email.Trim() });
    }

    [AllowAnonymous]
    public IActionResult ResetPassword(string? email) =>
        View(new ResetPasswordViewModel { Email = email?.Trim() ?? "" });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var reset = await _passwordReset.ResetPasswordAsync(
            model.Email, model.Code, model.NewPassword, cancellationToken);
        if (!reset)
        {
            ModelState.AddModelError("", "Mã OTP không hợp lệ, đã hết hạn hoặc đã được sử dụng. Hãy yêu cầu mã mới.");
            return View(model);
        }

        TempData["PasswordReset"] = "Đặt lại mật khẩu thành công. Hãy đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous] public IActionResult Denied() => View();
}
