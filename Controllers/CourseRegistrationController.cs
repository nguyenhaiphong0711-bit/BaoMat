using System.Security.Claims;
using LMS.Models;
using LMS.Services;
using LMS.Security;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Student), RequirePermission(PermissionCodes.CourseRegistrationView)]
public class CourseRegistrationController(
    AcademicService academics,
    UserService users,
    ClassService classes) : Controller
{
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();

        var available = await academics.GetEligibleClassesAsync(student);
        var registrationStatuses = await academics.GetRegistrationStatusesAsync(
            student.Id, available.Select(x => x.Class.Id).ToArray());
        var items = new List<CourseRegistrationItemViewModel>();
        foreach (var (classroom, subject, term) in available)
        {
            var teacher = await users.GetAsync(classroom.TeacherId.ToString());
            registrationStatuses.TryGetValue(classroom.Id, out var registrationStatus);
            items.Add(new CourseRegistrationItemViewModel
            {
                ClassId = classroom.Id.ToString(),
                ClassName = classroom.Name,
                SubjectCode = subject.Code,
                SubjectName = subject.Name,
                TeacherName = teacher?.FullName ?? "Chưa phân công",
                TermName = term.Name,
                Enrolled = classroom.StudentIds.Count,
                Capacity = classroom.EnrollmentCapacity,
                IsRegistered = classroom.StudentIds.Contains(student.Id) ||
                    registrationStatus == StudentRegistrationStatuses.Active,
                IsPending = registrationStatus == StudentRegistrationStatuses.Pending,
                RegistrationStatus = registrationStatus ?? ""
            });
        }

        ViewBag.Student = student;
        ViewBag.ActiveTerm = await academics.GetActiveTermAsync();
        ViewBag.HasProgram = student.ProgramId.HasValue;
        ViewBag.MyClasses = await classes.GetForUserAsync(student);
        var page = PaginationViewModel.Apply(items, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionCodes.CourseRegistrationManage)]
    public async Task<IActionResult> Register(string classId)
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();
        var result = await academics.RegisterAsync(student, classId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, RequirePermission(PermissionCodes.CourseRegistrationManage)]
    public async Task<IActionResult> Withdraw(string classId)
    {
        var student = await CurrentStudentAsync();
        if (student is null)
            return Forbid();
        var result = await academics.WithdrawAsync(student, classId);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<User?> CurrentStudentAsync()
    {
        var user = await users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");
        return user is { IsActive: true, Role: Roles.Student } ? user : null;
    }
}
