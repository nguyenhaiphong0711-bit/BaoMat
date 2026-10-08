using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class PermissionController(
    PermissionService permissions,
    UserService users,
    AcademicService academics,
    DepartmentService departments,
    SubjectService subjects) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? userId)
    {
        var eligibleUsers = (await users.GetAllAsync())
            .Where(user => user.Role is Roles.Teacher or Roles.Student)
            .ToList();
        var selected = eligibleUsers.FirstOrDefault(user => user.Id.ToString() == userId)
            ?? eligibleUsers.FirstOrDefault();
        var model = new PermissionManagementViewModel
        {
            Users = eligibleUsers,
            Permissions = await permissions.GetAllAsync(),
            Programs = await academics.GetProgramsAsync(),
            Departments = await departments.GetAllAsync(),
            Subjects = await subjects.GetAllAsync(),
            SelectedUser = selected ?? new User(),
            Assignment = selected is null ? new UserPermissionViewModel() : new UserPermissionViewModel
            {
                UserId = selected.Id.ToString(),
                PermissionCodes = selected.PermissionCodes.ToList(),
                DepartmentId = selected.DepartmentId?.ToString() ?? "",
                ProgramId = selected.ProgramId?.ToString() ?? "",
                CurrentSemester = selected.CurrentSemester,
                TeachingSubjectIds = selected.TeachingSubjectIds.Select(id => id.ToString()).ToList()
            }
        };
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(UserPermissionViewModel model)
    {
        if (!ModelState.IsValid ||
            !await permissions.AreValidCodesAsync(model.PermissionCodes) ||
            !await users.UpdateAcademicProfileAsync(model) ||
            !await permissions.SetForUserAsync(model.UserId, model.PermissionCodes))
        {
            TempData["Error"] = "Không thể lưu quyền hoặc hồ sơ học vụ. Kiểm tra khoa/ngành, môn giảng dạy và các quyền đã chọn.";
            return RedirectToAction(nameof(Index), new { userId = model.UserId });
        }

        TempData["Success"] = "Đã cập nhật quyền truy cập và hồ sơ học vụ cho tài khoản.";
        return RedirectToAction(nameof(Index), new { userId = model.UserId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PermissionDefinitionViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Thông tin quyền chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var created = await permissions.CreateAsync(new Permission
        {
            Code = model.Code,
            Name = model.Name,
            Category = model.Category,
            Description = model.Description,
            ControllerName = model.ControllerName,
            ActionName = model.ActionName
        });
        TempData[created ? "Success" : "Error"] = created
            ? "Đã thêm quyền mới. Gán quyền cho tài khoản để kích hoạt."
            : "Mã quyền bị trùng hoặc thông tin không hợp lệ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await permissions.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Đã xóa quyền tùy chỉnh."
            : "Không thể xóa quyền hệ thống hoặc quyền đang được tài khoản sử dụng.";
        return RedirectToAction(nameof(Index));
    }
}
