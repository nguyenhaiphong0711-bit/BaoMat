using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using MongoDB.Bson;
using LMS.Security;
namespace LMS.Controllers;
[Authorize] public class LessonController : Controller
{
    private readonly LessonService _lessons; private readonly ClassService _classes; private readonly UserService _users;
    public LessonController(LessonService l,ClassService c,UserService u){_lessons=l;_classes=c;_users=u;}
    [RequirePermission(PermissionCodes.LessonsView)]
    public async Task<IActionResult> Index(string classId, CatalogFilterViewModel filter)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId)) return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null || !await _classes.HasAccessAsync(classId, user)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        var lessons = await _lessons.GetByClassAsync(classObjectId, user.Role == Roles.Student);
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            lessons = lessons.Where(x => x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Content.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && bool.TryParse(filter.Status, out var published))
            lessons = lessons.Where(x => x.IsPublished == published).ToList();
        if (filter.CreatedFrom.HasValue)
            lessons = lessons.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            lessons = lessons.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        var page = PaginationViewModel.Apply(lessons, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Create(string classId)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
        ViewBag.ClassId = classId;
        return View(new LessonViewModel());
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),RequirePermission(PermissionCodes.LessonsManage),ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string classId, LessonViewModel vm)
    {
        if (!await CanManageClassAsync(classId)) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.ClassId = classId;
            ViewBag.ClassName = (await _classes.GetAsync(classId))?.Name ?? "";
            return View(vm);
        }
        await _lessons.CreateAsync(ObjectId.Parse(classId), vm);
        return RedirectToAction(nameof(Index), new { classId });
    }

    [Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.LessonsManage)]
    public async Task<IActionResult> Edit(string id)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null || lesson.IsArchived) return NotFound();
        if (!await CanManageClassAsync(lesson.ClassId.ToString())) return Forbid();
        ViewBag.ClassId = lesson.ClassId.ToString();
        ViewBag.ClassName = (await _classes.GetAsync(lesson.ClassId.ToString()))?.Name ?? "";
        return View(new LessonViewModel { Title = lesson.Title, Content = lesson.Content, IsPublished = lesson.IsPublished });
    }

    [HttpPost,Authorize(Roles=Roles.Admin+","+Roles.Teacher),RequirePermission(PermissionCodes.LessonsManage),ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, LessonViewModel vm)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null || lesson.IsArchived) return NotFound();
        if (!await CanManageClassAsync(lesson.ClassId.ToString())) return Forbid();
        if (!ModelState.IsValid)
        {
            ViewBag.ClassId = lesson.ClassId.ToString();
            ViewBag.ClassName = (await _classes.GetAsync(lesson.ClassId.ToString()))?.Name ?? "";
            return View(vm);
        }
        await _lessons.UpdateAsync(id, vm);
        return RedirectToAction(nameof(Index), new { classId = lesson.ClassId });
    }

    [HttpPost, Authorize(Roles=Roles.Admin+","+Roles.Teacher), RequirePermission(PermissionCodes.LessonsManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string id)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null) return NotFound();
        if (!await CanManageClassAsync(lesson.ClassId.ToString())) return Forbid();
        await _lessons.ArchiveAsync(id);
        TempData["Success"] = "Bài học đã được lưu trữ.";
        return RedirectToAction(nameof(Index), new { classId = lesson.ClassId });
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");

    private async Task<bool> CanManageClassAsync(string classId)
    {
        var user = await GetCurrentUserAsync();
        return user is not null && await _classes.HasAccessAsync(classId, user);
    }
}
