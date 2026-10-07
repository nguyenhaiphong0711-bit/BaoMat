using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using MongoDB.Bson;
namespace LMS.Controllers;
[Authorize] public class AssignmentController : Controller
{
    private readonly AssignmentService _assignments; private readonly ClassService _classes; private readonly UserService _users;
    public AssignmentController(AssignmentService a,ClassService c,UserService u){_assignments=a;_classes=c;_users=u;}
    public async Task<IActionResult> Index(string classId, CatalogFilterViewModel filter)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId)) return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null || !await _classes.HasAccessAsync(classId, user)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        var assignments = await _assignments.GetByClassAsync(classObjectId, user.Role == Roles.Student);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            assignments = assignments.Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && bool.TryParse(filter.Status, out var published))
            assignments = assignments.Where(x => x.IsPublished == published).ToList();
        if (filter.CreatedFrom.HasValue)
            assignments = assignments.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            assignments = assignments.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        if (filter.StartFrom.HasValue)
            assignments = assignments.Where(x => x.DueDate.Date >= filter.StartFrom.Value.Date).ToList();
        if (filter.StartTo.HasValue)
            assignments = assignments.Where(x => x.DueDate.Date <= filter.StartTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        return View(assignments);
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher)]
    public async Task<IActionResult> Create(string classId)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        return View(new AssignmentViewModel());
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string classId, AssignmentViewModel vm)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.ClassId = classId;
            ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
            return View(vm);
        }
        await _assignments.CreateAsync(ObjectId.Parse(classId), vm);
        return RedirectToAction(nameof(Index), new { classId });
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher)]
    public async Task<IActionResult> Edit(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        ViewBag.ClassId = assignment.ClassId.ToString();
        ViewBag.ClassName = (await _classes.GetAsync(assignment.ClassId.ToString()))?.Name ?? "";
        return View(new AssignmentViewModel
        {
            Title = assignment.Title,
            Description = assignment.Description,
            DueDate = assignment.DueDate,
            IsPublished = assignment.IsPublished
        });
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, AssignmentViewModel vm)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.ClassId = assignment.ClassId.ToString();
            ViewBag.ClassName = (await _classes.GetAsync(assignment.ClassId.ToString()))?.Name ?? "";
            return View(vm);
        }
        await _assignments.UpdateAsync(id, vm);
        return RedirectToAction(nameof(Index), new { classId = assignment.ClassId });
    }

    [HttpPost, Authorize(Roles=Roles.Admin+","+Roles.Teacher), ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null) return NotFound();
        if (!await CanManageClassAsync(assignment.ClassId.ToString())) return Forbid();
        await _assignments.ArchiveAsync(id);
        TempData["Success"] = "Bài tập đã được lưu trữ; các bài nộp và điểm vẫn được giữ.";
        return RedirectToAction(nameof(Index), new { classId = assignment.ClassId });
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");

    private async Task<bool> CanManageClassAsync(string classId)
    {
        var user = await GetCurrentUserAsync();
        return user is not null && await _classes.HasAccessAsync(classId, user);
    }
}
