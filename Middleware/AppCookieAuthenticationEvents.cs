using LMS.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using LMS.Models;

namespace LMS.Middleware;

public sealed class AppCookieAuthenticationEvents(UserService users) : CookieAuthenticationEvents
{
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
            return base.RedirectToLogin(context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Yêu cầu xác thực",
            Detail = "Vui lòng đăng nhập để truy cập tài nguyên này.",
            Instance = context.Request.Path
        });
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
            return base.RedirectToAccessDenied(context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Không đủ quyền truy cập",
            Detail = "Tài khoản hiện tại không có quyền thực hiện thao tác này.",
            Instance = context.Request.Path
        });
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.GetAsync(userId);
        var roleClaim = context.Principal?.FindFirstValue(ClaimTypes.Role);
        var ticketIssuedAt = context.Properties?.IssuedUtc?.UtcDateTime;

        if (user is { IsActive: true } &&
            user.Role == roleClaim &&
            (user.PasswordChangedAt is null || ticketIssuedAt >= user.PasswordChangedAt.Value))
        {
            if (context.Principal?.Identity is not ClaimsIdentity identity)
                return;

            var existing = identity.FindAll(PermissionCodes.ClaimType)
                .Select(claim => claim.Value)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var current = user.PermissionCodes
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (existing.SequenceEqual(current, StringComparer.Ordinal))
                return;

            foreach (var claim in identity.FindAll(PermissionCodes.ClaimType).ToArray())
                identity.RemoveClaim(claim);
            foreach (var code in current)
                identity.AddClaim(new Claim(PermissionCodes.ClaimType, code));
            context.ShouldRenew = true;
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
