using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class SubjectController(SubjectService subjects, DepartmentService departments, ClassService classes) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var departmentsList = await departments.GetAllAsync();
        var filtered = await subjects.SearchAsync(filter);
        var allClasses = await classes.GetAllAsync();
        return View(new SubjectCatalogViewModel
        {
            Filter = filter,
            Subjects = filtered,
            Departments = departmentsList,
            DepartmentNames = departmentsList.ToDictionary(x => x.Id.ToString(), x => x.Name),
            ClassCounts = allClasses.GroupBy(x => x.SubjectId.ToString())
                .ToDictionary(x => x.Key, x => x.Count())
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateDepartmentsAsync();
        return View("Form", new SubjectViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubjectViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync();
            return View("Form", model);
        }
        if (await subjects.CreateAsync(model) is null)
        {
            var errorField = await departments.GetAsync(model.DepartmentId) is null
                ? nameof(model.DepartmentId)
                : nameof(model.Code);
            ModelState.AddModelError(errorField, errorField == nameof(model.Code)
                ? "Mã môn học đã tồn tại."
                : "Vui lòng chọn khoa đang hoạt động.");
            await PopulateDepartmentsAsync();
            return View("Form", model);
        }
        TempData["Success"] = "Đã tạo môn học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var subject = await subjects.GetAsync(id);
        if (subject is null)
            return NotFound();
        await PopulateDepartmentsAsync();
        return View("Form", new SubjectViewModel
        {
            DepartmentId = subject.DepartmentId.ToString(),
            Code = subject.Code,
            Name = subject.Name,
            Description = subject.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SubjectViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync();
            return View("Form", model);
        }
        if (!await subjects.UpdateAsync(id, model))
        {
            var errorField = await departments.GetAsync(model.DepartmentId) is null
                ? nameof(model.DepartmentId)
                : nameof(model.Code);
            ModelState.AddModelError(errorField, errorField == nameof(model.Code)
                ? "Không thể cập nhật môn học; mã đã được sử dụng."
                : "Vui lòng chọn khoa đang hoạt động.");
            await PopulateDepartmentsAsync();
            return View("Form", model);
        }
        TempData["Success"] = "Đã cập nhật môn học.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await subjects.DeleteAsync(id);
        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = "Đã xóa môn học.";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateDepartmentsAsync() =>
        ViewBag.Departments = await departments.GetAllAsync();
}
