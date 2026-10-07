using LMS.Models; using LMS.Services; using LMS.ViewModels; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
namespace LMS.Controllers;
[Authorize] public class ClassController : Controller
{
    private readonly ClassService _classes; private readonly UserService _users; private readonly SubjectService _subjects; private readonly DepartmentService _departments;
    public ClassController(ClassService classes, UserService users, SubjectService subjects, DepartmentService departments){_classes=classes;_users=users;_subjects=subjects;_departments=departments;}
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _users.GetAsync(uid ?? "");
        var classroomList = user is null ? new List<ClassRoom>() : await _classes.GetForUserAsync(user);
        var allSubjects = await _subjects.GetAllAsync();
        var departments = await _departments.GetAllAsync();
        var allUsers = await _users.GetAllAsync();
        var subjectById = allSubjects.ToDictionary(x => x.Id.ToString());
        var departmentIds = departments.ToDictionary(x => x.Id.ToString(), x => x.Name);
        var userById = allUsers.ToDictionary(x => x.Id.ToString());
        var query = filter.Query.Trim();
        if (!string.IsNullOrEmpty(query))
            classroomList = classroomList.Where(classroom =>
            {
                var subject = subjectById.GetValueOrDefault(classroom.SubjectId.ToString());
                var teacher = userById.GetValueOrDefault(classroom.TeacherId.ToString());
                return classroom.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (subject?.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (teacher?.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    classroom.StudentIds.Any(studentId => userById.GetValueOrDefault(studentId.ToString())?.FullName.Contains(query, StringComparison.OrdinalIgnoreCase) == true);
            }).ToList();
        if (ObjectId.TryParse(filter.DepartmentId, out var departmentId))
            classroomList = classroomList.Where(x => subjectById.GetValueOrDefault(x.SubjectId.ToString())?.DepartmentId == departmentId).ToList();
        if (ObjectId.TryParse(filter.SubjectId, out var subjectId))
            classroomList = classroomList.Where(x => x.SubjectId == subjectId).ToList();
        if (filter.CreatedFrom.HasValue)
            classroomList = classroomList.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            classroomList = classroomList.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        ViewBag.SubjectNames = allSubjects.ToDictionary(x => x.Id, x => $"{x.Code} · {x.Name}");
        ViewBag.TeacherNames = allUsers.ToDictionary(x => x.Id.ToString(), x => x.FullName);
        ViewBag.DepartmentNames = departmentIds;
        ViewBag.Subjects = allSubjects;
        ViewBag.Departments = departments;
        ViewBag.Filter = filter;
        return View(classroomList);
    }
    [Authorize(Roles=Roles.Admin)] public async Task<IActionResult> Create(){await PopulateClassChoicesAsync();return View(new CreateClassViewModel());}
    [HttpPost,Authorize(Roles=Roles.Admin),ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateClassViewModel vm)
    {
        if (ModelState.IsValid &&
            (!await HasValidMembersAsync(vm.TeacherId, vm.StudentIds) ||
             await _subjects.GetAsync(vm.SubjectId) is null ||
             !await _classes.CanTeachSubjectAsync(vm.TeacherId, vm.SubjectId)))
            ModelState.AddModelError("", "Chọn môn học hợp lệ và giảng viên đã được phân công dạy môn đó; kiểm tra lại danh sách học viên.");

        if (!ModelState.IsValid)
        {
            await PopulateClassChoicesAsync();
            return View(vm);
        }

        await _classes.CreateAsync(vm);
        return RedirectToAction(nameof(Index));
    }
    [Authorize(Roles=Roles.Admin)] public async Task<IActionResult> Edit(string id)
    {
        var classroom = await _classes.GetAsync(id);
        if (classroom is null) return NotFound();

        await PopulateClassChoicesAsync();
        return View(new UpdateClassViewModel
        {
            Name = classroom.Name,
            Description = classroom.Description,
            SubjectId = classroom.SubjectId.ToString(),
            TeacherId = classroom.TeacherId.ToString(),
            StudentIds = classroom.StudentIds.Select(studentId => studentId.ToString()).ToList()
        });
    }

    [HttpPost,Authorize(Roles=Roles.Admin),ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UpdateClassViewModel vm)
    {
        if (!ModelState.IsValid || !await _classes.UpdateAsync(id, vm))
        {
            if (ModelState.IsValid)
                ModelState.AddModelError("", "Giáo viên hoặc học viên không hợp lệ, hoặc lớp học không tồn tại.");
            await PopulateClassChoicesAsync();
            return View(vm);
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost,Authorize(Roles=Roles.Admin),ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string id)
    {
        var result = await _classes.ArchiveAsync(id);
        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = "Lớp đã được lưu trữ. Lịch sử bài nộp và buổi học vẫn còn.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(string id)
    {
        var classroom = await _classes.GetAsync(id);
        if (classroom is null) return NotFound();
        var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _users.GetAsync(uid ?? "");
        if(user == null||!await _classes.HasAccessAsync(id,user))return Forbid();
        ViewBag.TeacherName = (await _users.GetAsync(classroom.TeacherId.ToString()))?.FullName ?? "Unknown";
        ViewBag.SubjectName = (await _subjects.GetAsync(classroom.SubjectId.ToString()))?.Name ?? "Chưa gán môn";
        ViewBag.Students = await _users.GetByIdsAsync(classroom.StudentIds.ToArray());
        return View(classroom);
    }

    private async Task PopulateClassChoicesAsync()
    {
        ViewBag.Teachers = await _users.GetTeachersAsync();
        ViewBag.Students = await _users.GetStudentsAsync();
        ViewBag.Subjects = await _subjects.GetAllAsync();
        ViewBag.Departments = await _departments.GetAllAsync();
    }

    private async Task<bool> HasValidMembersAsync(string teacherId, IReadOnlyCollection<string>? studentIds)
    {
        var teacher = await _users.GetAsync(teacherId);
        if (teacher is not { IsActive: true, Role: Roles.Teacher } || studentIds is null)
            return false;

        foreach (var studentId in studentIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var student = await _users.GetAsync(studentId);
            if (student is not { IsActive: true, Role: Roles.Student })
                return false;
        }

        return true;
    }
}
