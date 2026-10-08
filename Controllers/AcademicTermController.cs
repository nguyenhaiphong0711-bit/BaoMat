using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace LMS.Controllers;

[Authorize(Roles = Roles.Admin)]
public class AcademicTermController(AcademicService academics) : Controller
{
    public async Task<IActionResult> Index(string? id, int page = 1, int pageSize = 20)
    {
        var terms = await academics.GetTermsAsync();
        ViewBag.Terms = PaginationViewModel.Apply(terms, page, pageSize, out var pagination);
        ViewBag.Pagination = pagination;
        if (!ObjectId.TryParse(id, out var termId))
            return View(new AcademicTermViewModel());

        var term = terms.FirstOrDefault(x => x.Id == termId);
        if (term is null)
            return NotFound();

        return View(new AcademicTermViewModel
        {
            Id = term.Id.ToString(),
            Code = term.Code,
            Name = term.Name,
            AcademicYear = term.AcademicYear,
            SemesterNumber = term.SemesterNumber,
            StartsAt = term.StartsAt,
            EndsAt = term.EndsAt,
            RegistrationOpensAt = term.RegistrationOpensAt,
            RegistrationClosesAt = term.RegistrationClosesAt,
            TuitionPerCredit = term.TuitionPerCredit,
            IsActive = term.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AcademicTermViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Terms = await academics.GetTermsAsync();
            return View("Index", model);
        }

        var saved = await academics.SaveTermAsync(new AcademicTerm
        {
            Id = ObjectId.TryParse(model.Id, out var id) ? id : ObjectId.Empty,
            Code = model.Code,
            Name = model.Name,
            AcademicYear = model.AcademicYear,
            SemesterNumber = model.SemesterNumber,
            StartsAt = model.StartsAt,
            EndsAt = model.EndsAt,
            RegistrationOpensAt = model.RegistrationOpensAt,
            RegistrationClosesAt = model.RegistrationClosesAt,
            TuitionPerCredit = model.TuitionPerCredit,
            IsActive = model.IsActive
        });
        if (!saved)
        {
            TempData["Error"] = "Không thể lưu học kỳ. Kiểm tra thời gian, mã học kỳ và ngày đóng đăng ký.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Đã lưu học kỳ.";
        return RedirectToAction(nameof(Index));
    }
}
