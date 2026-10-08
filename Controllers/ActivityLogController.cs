using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace LMS.Controllers;
[Authorize(Roles=Roles.Admin)]
public class ActivityLogController : Controller
{
    private readonly ActivityLogService _service;
    public ActivityLogController(ActivityLogService service) => _service = service;

    public async Task<IActionResult> Index(CatalogFilterViewModel filter)
    {
        var logs = await _service.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            logs = logs.Where(x => x.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Action.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.Message.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.IpAddress.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        if (!string.IsNullOrWhiteSpace(filter.Status) && bool.TryParse(filter.Status, out var succeeded))
            logs = logs.Where(x => x.Success == succeeded).ToList();
        if (filter.CreatedFrom.HasValue)
            logs = logs.Where(x => x.CreatedAt.Date >= filter.CreatedFrom.Value.Date).ToList();
        if (filter.CreatedTo.HasValue)
            logs = logs.Where(x => x.CreatedAt.Date <= filter.CreatedTo.Value.Date).ToList();
        ViewBag.Filter = filter;
        var page = PaginationViewModel.Apply(logs, filter.Page, filter.PageSize, out var pagination);
        ViewBag.Pagination = pagination;
        return View(page);
    }
}
