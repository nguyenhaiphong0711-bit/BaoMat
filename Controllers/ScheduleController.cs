using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace LMS.Controllers;

[Authorize]
public sealed class ScheduleController(
    ScheduleService schedules,
    UserService users,
    ClassService classes,
    SubjectService subjects) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CatalogFilterViewModel filter, string? classId)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        var sessions = await schedules.GetSessionsAsync(user);
        if (!string.IsNullOrWhiteSpace(classId))
        {
            if (!ObjectId.TryParse(classId, out _))
                return NotFound();
            if (!await classes.HasAccessAsync(classId, user))
                return Forbid();
            sessions = sessions.Where(x => x.ClassId == classId).ToList();
            ViewBag.ClassId = classId;
        }
        if (ObjectId.TryParse(filter.ClassId, out var selectedClassId))
            sessions = sessions.Where(x => x.ClassId == selectedClassId.ToString()).ToList();
        if (ObjectId.TryParse(filter.SubjectId, out var selectedSubjectId))
            sessions = sessions.Where(x => x.SubjectId == selectedSubjectId.ToString()).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Status))
            sessions = sessions.Where(x => x.Status.Equals(filter.Status, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filter.StartFrom.HasValue)
            sessions = sessions.Where(x => x.StartAt.Date >= filter.StartFrom.Value.Date).ToList();
        if (filter.StartTo.HasValue)
            sessions = sessions.Where(x => x.StartAt.Date <= filter.StartTo.Value.Date).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var query = filter.Query.Trim();
            sessions = sessions.Where(x => x.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.SubjectName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                x.TeacherName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ViewBag.Filter = filter;
        ViewBag.Classes = await classes.GetForUserAsync(user);
        ViewBag.Subjects = await subjects.GetAllAsync();
        return View(sessions);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Availability()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        return View(await BuildAvailabilityPageAsync(user));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> Availability(TeacherAvailabilityPageViewModel page)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        if (!ModelState.IsValid)
            return View(await BuildAvailabilityPageAsync(user, page.Request));

        var result = await schedules.AddAvailabilityAsync(user.Id, page.Request);
        if (result.Availability is null)
        {
            ModelState.AddModelError("", result.Error ?? "Không thể gửi lịch rảnh.");
            return View(await BuildAvailabilityPageAsync(user, page.Request));
        }
        TempData["Success"] = "Đã gửi khung giờ rảnh để admin duyệt.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Teacher)]
    public async Task<IActionResult> CancelAvailability(string id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        if (!await schedules.CancelAvailabilityAsync(id, user.Id))
            TempData["Error"] = "Chỉ có thể rút lịch đang chờ duyệt của chính bạn.";
        else
            TempData["Success"] = "Đã rút khung giờ đang chờ duyệt.";
        return RedirectToAction(nameof(Availability));
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AvailabilityRequests()
    {
        var requests = await schedules.GetAvailabilitiesAsync();
        var teacherIds = requests.Select(x => x.TeacherId).Distinct().ToArray();
        var teachers = await users.GetByIdsAsync(teacherIds);
        var names = teachers.ToDictionary(x => x.Id, x => x.FullName);
        return View(requests.Select(request => new AvailabilityListItem
        {
            Id = request.Id.ToString(),
            TeacherName = names.GetValueOrDefault(request.TeacherId, "Unknown teacher"),
            StartAt = AsUtcOffset(request.StartAt),
            EndAt = AsUtcOffset(request.EndAt),
            Note = request.Note,
            Status = request.Status
        }).ToList());
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ReviewAvailability(string id, bool approve)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        if (!await schedules.ReviewAvailabilityAsync(id, user.Id, approve))
            TempData["Error"] = "Không thể thay đổi yêu cầu này; có thể yêu cầu đã được xử lý.";
        else
            TempData["Success"] = approve ? "Đã duyệt khung giờ giảng dạy." : "Đã từ chối khung giờ giảng dạy.";
        return RedirectToAction(nameof(AvailabilityRequests));
    }

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CreateSession(string? classId)
    {
        var model = await BuildPlannerAsync(classId);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CreateSession(SchedulePlannerViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPlannerAsync(vm.Request.ClassId);
            invalidModel.Request = vm.Request;
            return View(invalidModel);
        }

        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        var result = await schedules.CreateSessionAsync(
            vm.Request.ClassId,
            vm.Request.SubjectId,
            vm.Request.StartAt,
            vm.Request.EndAt,
            vm.Request.Capacity,
            user.Id);
        if (result.Session is null)
        {
            ModelState.AddModelError("", result.Error ?? "Không thể tạo buổi học.");
            var invalidModel = await BuildPlannerAsync(vm.Request.ClassId);
            invalidModel.Request = vm.Request;
            return View(invalidModel);
        }

        TempData["Success"] = "Đã phân công giảng viên và mở đăng ký cho lớp.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        var details = await schedules.GetSessionDetailsAsync(id, user);
        return details is null ? NotFound() : View(details);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Enroll(string id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        var result = await schedules.EnrollAsync(id, user);
        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = "Đăng ký buổi học thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> Withdraw(string id)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Forbid();
        var result = await schedules.CancelEnrollmentAsync(id, user);
        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = "Đã hủy đăng ký. Lịch sử vẫn được lưu.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    public async Task<IActionResult> Cancel(string id, CancelSessionViewModel vm)
    {
        var user = await CurrentUserAsync();
        var session = await schedules.GetSessionAsync(id);
        if (session is null || user is null)
            return NotFound();
        if (user.Role == Roles.Teacher && session.TeacherId != user.Id)
            return Forbid();
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Lý do hủy phải có ít nhất 3 ký tự.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (!await schedules.CancelSessionAsync(id, vm.Reason))
            TempData["Error"] = "Buổi học không còn ở trạng thái có thể hủy.";
        else
            TempData["Success"] = "Buổi học đã được hủy; đăng ký học viên được lưu trong lịch sử.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<SchedulePlannerViewModel> BuildPlannerAsync(string? selectedClassId)
    {
        var allClasses = await classes.GetAllAsync();
        var subjectList = await subjects.GetAllAsync();
        var subjectNames = subjectList.ToDictionary(x => x.Id, x => x.Name);
        var teacherIds = allClasses.Select(x => x.TeacherId).Distinct().ToArray();
        var teachers = await users.GetByIdsAsync(teacherIds);
        var teacherById = teachers.ToDictionary(x => x.Id);
        var eligibleClasses = allClasses.Where(classroom =>
            subjectNames.ContainsKey(classroom.SubjectId) &&
            teacherById.TryGetValue(classroom.TeacherId, out var teacher) &&
            teacher.IsActive &&
            teacher.TeachingSubjectIds.Contains(classroom.SubjectId)).ToList();
        var selectedClass = eligibleClasses.FirstOrDefault(x => x.Id.ToString() == selectedClassId)
            ?? eligibleClasses.FirstOrDefault();
        var teacherName = selectedClass is not null &&
            teacherById.TryGetValue(selectedClass.TeacherId, out var selectedTeacher)
                ? selectedTeacher.FullName
                : "";

        return new SchedulePlannerViewModel
        {
            Request = new ScheduleSessionViewModel
            {
                ClassId = selectedClass?.Id.ToString() ?? "",
                SubjectId = selectedClass?.SubjectId.ToString() ?? ""
            },
            Classes = eligibleClasses.Select(classroom => new TeachingAssignmentListItem
            {
                ClassId = classroom.Id.ToString(),
                ClassName = classroom.Name,
                TeacherId = classroom.TeacherId.ToString(),
                TeacherName = teacherById.GetValueOrDefault(classroom.TeacherId)?.FullName ?? "",
                SubjectName = subjectNames.GetValueOrDefault(classroom.SubjectId, ""),
                SubjectId = classroom.SubjectId.ToString()
            }).ToList(),
            Subjects = subjectList,
            TeacherName = teacherName
        };
    }

    private async Task<TeacherAvailabilityPageViewModel> BuildAvailabilityPageAsync(
        User teacher,
        TeacherAvailabilityViewModel? request = null)
    {
        var availability = await schedules.GetAvailabilitiesAsync(teacher.Id);
        return new TeacherAvailabilityPageViewModel
        {
            Request = request ?? new TeacherAvailabilityViewModel(),
            Requests = availability.Select(item => new AvailabilityListItem
            {
                Id = item.Id.ToString(),
                TeacherName = teacher.FullName,
                StartAt = AsUtcOffset(item.StartAt),
                EndAt = AsUtcOffset(item.EndAt),
                Note = item.Note,
                Status = item.Status
            }).ToList()
        };
    }

    private Task<User?> CurrentUserAsync() =>
        users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    private static DateTimeOffset AsUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
