using System.Security.Claims;
using LMS.Models;
using LMS.Security;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Student)]
public sealed class StudentAcademicController(
    UserService users,
    StudentAcademicService academics,
    VnpayPaymentService vnpay,
    Microsoft.Extensions.Options.IOptions<VnpaySettings> vnpayOptions,
    IHostEnvironment environment) : Controller
{
    [RequirePermission(PermissionCodes.StudentProgressView)]
    public async Task<IActionResult> Progress()
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();

        return View(await academics.GetDashboardAsync(student));
    }

    [RequirePermission(PermissionCodes.TuitionView)]
    public async Task<IActionResult> Tuition()
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();

        return View(await academics.GetDashboardAsync(student));
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionCodes.TuitionPay)]
    public async Task<IActionResult> Pay(
        string classId,
        CancellationToken cancellationToken)
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();

        var configuredReturnUrl = vnpayOptions.Value.ReturnUrl;
        var returnUrl = string.IsNullOrWhiteSpace(configuredReturnUrl)
            ? Url.Action(nameof(VnpayController.Return), "Vnpay", null, Request.Scheme)!
            : configuredReturnUrl;
        var configuredIpnUrl = vnpayOptions.Value.IpnUrl;
        var ipnUrl = string.IsNullOrWhiteSpace(configuredIpnUrl)
            ? Url.Action(nameof(VnpayController.Ipn), "Vnpay", null, Request.Scheme)
            : configuredIpnUrl;
        var result = await academics.CreatePaymentUrlAsync(
            student,
            classId,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
            returnUrl,
            ipnUrl,
            vnpay,
            environment.IsProduction(),
            cancellationToken);

        if (!result.Success || result.PaymentUrl is null)
        {
            TempData["Error"] = result.Message;
            return RedirectToAction(nameof(Tuition));
        }

        return Redirect(result.PaymentUrl);
    }

    private async Task<User?> CurrentStudentAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.GetAsync(userId);
        return user is { IsActive: true, Role: Roles.Student } ? user : null;
    }
}
