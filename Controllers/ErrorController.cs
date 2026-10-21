using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [Route("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        var path = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path;
        if (path?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true ||
            path?.Equals("/Vnpay/Ipn", StringComparison.OrdinalIgnoreCase) == true)
            return Problem(statusCode: 500, title: "Hệ thống chưa thể hoàn thành yêu cầu.");
        return View("~/Views/Shared/Error.cshtml");
    }
}
