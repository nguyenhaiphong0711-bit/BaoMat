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
    private readonly AuthService _auth; public AuthController(AuthService auth) => _auth = auth;
    [AllowAnonymous] public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View();
    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var result = await _auth.LoginAsync(vm, HttpContext);
        if (!result.Success) { ModelState.AddModelError("", result.Message); return View(vm); }
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, result.User!.Id.ToString()), new Claim(ClaimTypes.Name, result.User.FullName), new Claim(ClaimTypes.Email, result.User.Email), new Claim(ClaimTypes.Role, result.User.Role) };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return RedirectToAction("Index", "Dashboard");
    }
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
    [AllowAnonymous] public IActionResult Denied() => View();
}
