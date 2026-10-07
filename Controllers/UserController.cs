using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
namespace LMS.Controllers;
[Authorize(Roles=Roles.Admin)] public class UserController : Controller
{
    private readonly UserService _service; public UserController(UserService service) => _service=service;
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var users = await _service.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            users = users.Where(x => x.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Email.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Role) &&
            new[] { Roles.Admin, Roles.Teacher, Roles.Student }.Contains(filter.Role, StringComparer.OrdinalIgnoreCase))
            users = users.Where(x => x.Role.Equals(filter.Role, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Status) && bool.TryParse(filter.Status, out var isActive))
            users = users.Where(x => x.IsActive == isActive).ToList();
        if (filter.CreatedFrom.HasValue)
            users = users.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            users = users.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        return View(users);
    }
    public IActionResult Create() => View(new CreateUserViewModel());
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Create(CreateUserViewModel vm) { if(!ModelState.IsValid) return View(vm); if(!await _service.CreateAsync(vm)){ModelState.AddModelError("Email","Email đã tồn tại.");return View(vm);} return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Toggle(string id)
    {
        if (id == User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)
            return Forbid();

        await _service.ToggleAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
