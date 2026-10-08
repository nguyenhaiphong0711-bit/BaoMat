using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[AllowAnonymous]
public sealed class VnpayController(
    VnpayPaymentService vnpay,
    StudentAcademicService academics,
    ILogger<VnpayController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Return(CancellationToken cancellationToken)
    {
        var result = await academics.ProcessCallbackAsync(vnpay.ReadCallback(Request.Query), cancellationToken);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        if (!result.Success)
            logger.LogWarning("VNPAY return did not confirm payment: {Reason}", result.Message);
        return RedirectToAction(nameof(StudentAcademicController.Tuition), "StudentAcademic");
    }

    [HttpGet]
    public async Task<IActionResult> Ipn(CancellationToken cancellationToken)
    {
        var callback = vnpay.ReadCallback(Request.Query);
        var result = await academics.ProcessCallbackAsync(callback, cancellationToken);
        var responseCode = result.ResponseCode;
        var message = responseCode == "00" ? "Confirm Success" : "Confirm Failed";
        if (!result.Success)
            logger.LogWarning("VNPAY IPN rejected: {Reason}", result.Message);

        return new JsonResult(new { RspCode = responseCode, Message = message });
    }
}
