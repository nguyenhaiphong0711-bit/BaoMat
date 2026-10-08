using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class DepartmentController(DepartmentService departments, SubjectService subjects) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var list = await departments.GetAllAsync();
        var query = filter.Query.Trim();
        if (!string.IsNullOrEmpty(query))
            list = list.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filter.CreatedFrom.HasValue)
            list = list.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            list = list.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        var allSubjects = await subjects.GetAllAsync();
        ViewBag.SubjectCounts = allSubjects.GroupBy(x => x.DepartmentId.ToString())
            .ToDictionary(x => x.Key, x => x.Count());
        ViewBag.Filter = filter;
        var page = PaginationViewModel.Apply(list, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new DepartmentViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Form", model);
        if (await departments.CreateAsync(model) is null)
        {
            ModelState.AddModelError(nameof(model.Code), "Mã khoa đã tồn tại.");
            return View("Form", model);
        }
        TempData["Success"] = "Đã tạo khoa.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var department = await departments.GetAsync(id);
        return department is null ? NotFound() : View("Form", new DepartmentViewModel
        {
            Code = department.Code,
            Name = department.Name,
            Description = department.Description
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, DepartmentViewModel model)
    {
        if (!ModelState.IsValid)
            return View("Form", model);
        if (!await departments.UpdateAsync(id, model))
        {
            ModelState.AddModelError(nameof(model.Code), "Không thể cập nhật khoa; mã có thể đã được sử dụng.");
            return View("Form", model);
        }
        TempData["Success"] = "Đã cập nhật khoa.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await departments.DeleteAsync(id);
        TempData[result.Success ? "Success" : "Error"] = result.Success ? "Đã xóa khoa." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}
