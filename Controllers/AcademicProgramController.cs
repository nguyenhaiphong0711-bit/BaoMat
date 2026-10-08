using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AcademicProgramController(
    AcademicService academics,
    DepartmentService departments,
    SubjectService subjects) : Controller
{
    public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
    {
        await PopulateChoicesAsync();
        var programs = await academics.GetAllProgramsAsync();
        ViewBag.Programs = PaginationViewModel.Apply(programs, page, pageSize, out var pagination);
        ViewBag.Pagination = pagination;
        ViewBag.ProgramCourseCounts = await academics.GetProgramCourseCountsAsync();
        return View(new AcademicProgramViewModel());
    }

    public async Task<IActionResult> Edit(string id, int page = 1, int pageSize = 20)
    {
        var program = await academics.GetProgramAsync(id);
        if (program is null)
            return NotFound();

        await PopulateChoicesAsync();
        var programs = await academics.GetAllProgramsAsync();
        ViewBag.Programs = PaginationViewModel.Apply(programs, page, pageSize, out var pagination);
        ViewBag.Pagination = pagination;
        ViewBag.ProgramCourseCounts = await academics.GetProgramCourseCountsAsync();
        var assigned = await academics.GetProgramCurriculumAsync(id);
        var assignedBySubject = assigned.ToDictionary(x => x.SubjectId);
        return View("Index", new AcademicProgramViewModel
        {
            Id = program.Id.ToString(),
            DepartmentId = program.DepartmentId.ToString(),
            Code = program.Code,
            Name = program.Name,
            Description = program.Description,
            Curriculum = (await subjects.GetAllAsync()).Select(subject =>
                assignedBySubject.TryGetValue(subject.Id.ToString(), out var item)
                    ? item
                    : new ProgramCourseViewModel { SubjectId = subject.Id.ToString() }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AcademicProgramViewModel model)
    {
        if (!ObjectId.TryParse(model.DepartmentId, out var departmentId))
            ModelState.AddModelError(nameof(model.DepartmentId), "Chọn khoa hợp lệ.");
        if (!ModelState.IsValid)
        {
            await PopulateChoicesAsync();
            ViewBag.Programs = await academics.GetAllProgramsAsync();
            return View("Index", model);
        }

        var saved = await academics.SaveProgramAsync(new AcademicProgram
        {
            Id = ObjectId.TryParse(model.Id, out var id) ? id : ObjectId.Empty,
            DepartmentId = departmentId,
            Code = model.Code,
            Name = model.Name,
            Description = model.Description
        }, model.Curriculum);
        if (!saved)
        {
            TempData["Error"] = "Không thể lưu ngành học. Kiểm tra mã, khoa, các môn thuộc đúng khoa hoặc hồ sơ sinh viên đang sử dụng ngành.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã lưu ngành học và danh sách môn theo học kỳ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string id, bool isActive)
    {
        var result = await academics.SetProgramActiveAsync(id, isActive);
        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateChoicesAsync()
    {
        ViewBag.Departments = await departments.GetAllAsync();
        ViewBag.Subjects = await subjects.GetAllAsync();
    }
}
