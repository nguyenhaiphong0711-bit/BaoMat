using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class TeachingAssignmentController(ClassService classes, UserService users, SubjectService subjects, DepartmentService departments) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var classrooms = await classes.GetAllAsync();
        var teachers = await users.GetTeachersAsync();
        var allSubjects = await subjects.GetAllAsync();
        var teacherNames = teachers.ToDictionary(teacher => teacher.Id.ToString(), teacher => teacher.FullName);
        var subjectNames = allSubjects.ToDictionary(subject => subject.Id.ToString(), subject => subject.Name);
        var departmentList = await departments.GetAllAsync();
        var departmentBySubject = allSubjects.ToDictionary(subject => subject.Id.ToString(), subject => subject.DepartmentId.ToString());

        var rows = classrooms.Select(classroom => new TeachingAssignmentListItem
        {
            ClassId = classroom.Id.ToString(),
            ClassName = classroom.Name,
            TeacherId = classroom.TeacherId.ToString(),
            TeacherName = teacherNames.GetValueOrDefault(classroom.TeacherId.ToString(), "No active teacher assigned"),
            SubjectId = classroom.SubjectId.ToString(),
            DepartmentId = departmentBySubject.GetValueOrDefault(classroom.SubjectId.ToString(), ""),
            CreatedAt = classroom.CreatedAt,
            SubjectName = subjectNames.GetValueOrDefault(classroom.SubjectId.ToString(), "Chưa gán môn")
        }).ToList();
        if (ObjectId.TryParse(filter.DepartmentId, out var departmentId))
            rows = rows.Where(x => x.DepartmentId == departmentId.ToString()).ToList();
        if (ObjectId.TryParse(filter.SubjectId, out var subjectId))
            rows = rows.Where(x => x.SubjectId == subjectId.ToString()).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            rows = rows.Where(x => x.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.SubjectName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.TeacherName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (filter.CreatedFrom.HasValue)
            rows = rows.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            rows = rows.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        ViewBag.Departments = departmentList;
        ViewBag.Subjects = allSubjects;
        var page = PaginationViewModel.Apply(rows, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [HttpGet]
    public async Task<IActionResult> Assign(string classId)
    {
        var classroom = await classes.GetAsync(classId);
        if (classroom is null) return NotFound();

        ViewBag.ClassName = classroom.Name;
        ViewBag.Teachers = (await users.GetTeachersAsync())
            .Where(teacher => teacher.TeachingSubjectIds.Contains(classroom.SubjectId)).ToList();
        ViewBag.SubjectName = (await subjects.GetAsync(classroom.SubjectId.ToString()))?.Name ?? "Chưa gán môn";
        return View(new TeachingAssignmentViewModel
        {
            ClassId = classId,
            TeacherId = classroom.TeacherId.ToString(),
            SubjectId = classroom.SubjectId.ToString()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(string classId, TeachingAssignmentViewModel vm)
    {
        vm.ClassId = classId;
        var classroom = await classes.GetAsync(classId);
        if (!ModelState.IsValid || classroom is null ||
            vm.SubjectId != classroom.SubjectId.ToString() ||
            !await classes.AssignTeacherAsync(classId, vm.TeacherId))
        {
            if (ModelState.IsValid)
                ModelState.AddModelError("", "Giảng viên phải đang hoạt động và được phân công dạy môn của lớp.");
            if (classroom is null) return NotFound();
            ViewBag.ClassName = classroom.Name;
            ViewBag.SubjectName = (await subjects.GetAsync(classroom.SubjectId.ToString()))?.Name ?? "Chưa gán môn";
            ViewBag.Teachers = (await users.GetTeachersAsync())
                .Where(teacher => teacher.TeachingSubjectIds.Contains(classroom.SubjectId)).ToList();
            return View(vm);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> TeacherSubjects(string? teacherId)
    {
        var teachers = await users.GetTeachersAsync();
        var selected = teachers.FirstOrDefault(x => x.Id.ToString() == teacherId) ?? teachers.FirstOrDefault();
        ViewBag.Teachers = teachers;
        ViewBag.Subjects = await subjects.GetAllAsync();
        ViewBag.Departments = await departments.GetAllAsync();
        return View(new TeacherSubjectsViewModel
        {
            TeacherId = selected?.Id.ToString() ?? "",
            SubjectIds = selected?.TeachingSubjectIds.Select(x => x.ToString()).ToList() ?? new()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TeacherSubjects(TeacherSubjectsViewModel model)
    {
        if (!ModelState.IsValid || !await users.AssignTeachingSubjectsAsync(model.TeacherId, model.SubjectIds))
        {
            ModelState.AddModelError("", "Chọn giáo viên đang hoạt động và các môn học hợp lệ.");
            ViewBag.Teachers = await users.GetTeachersAsync();
            ViewBag.Subjects = await subjects.GetAllAsync();
            ViewBag.Departments = await departments.GetAllAsync();
            return View(model);
        }
        TempData["Success"] = "Đã cập nhật danh sách môn được giảng viên giảng dạy.";
        return RedirectToAction(nameof(TeacherSubjects), new { teacherId = model.TeacherId });
    }
}
