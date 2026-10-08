using LMS.Models;
using LMS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class CourseRegistrationAdminController(AcademicService academics) : Controller
{
    public async Task<IActionResult> Index()
    {
        return View(await academics.GetPendingRegistrationReviewsAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string registrationId, bool approve)
    {
        var result = await academics.ReviewRegistrationAsync(registrationId, approve);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}
