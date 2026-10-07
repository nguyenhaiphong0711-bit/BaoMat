using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
namespace LMS.Controllers;
[Authorize] public class DashboardController : Controller { public IActionResult Index() => View(); }
