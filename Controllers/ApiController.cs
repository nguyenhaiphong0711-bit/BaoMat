using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using System.Security.Claims;

namespace LMS.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class ApiController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly UserService _users;
    private readonly ClassService _classes;
    private readonly SubjectService _subjects;
    private readonly DepartmentService _departments;
    private readonly LessonService _lessons;
    private readonly AssignmentService _assignments;
    private readonly SubmissionService _submissions;
    private readonly ActivityLogService _activityLogs;
    private readonly ScheduleService _schedules;

    public ApiController(
        AuthService auth,
        UserService users,
        ClassService classes,
        SubjectService subjects,
        DepartmentService departments,
        LessonService lessons,
        AssignmentService assignments,
        SubmissionService submissions,
        ActivityLogService activityLogs,
        ScheduleService schedules)
    {
        _auth = auth;
        _users = users;
        _classes = classes;
        _subjects = subjects;
        _departments = departments;
        _lessons = lessons;
        _assignments = assignments;
        _submissions = submissions;
        _activityLogs = activityLogs;
        _schedules = schedules;
    }

    [AllowAnonymous]
    [HttpPost("auth/login")]
    [ProducesResponseType<ApiIdentityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(ApiLoginRequest request)
    {
        var result = await _auth.LoginAsync(
            new LoginViewModel { Email = request.Email, Password = request.Password },
            HttpContext);

        if (!result.Success)
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Đăng nhập thất bại",
                Detail = result.Message,
                Instance = HttpContext.Request.Path
            });

        var user = result.User!;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return Ok(ToIdentityResponse(user));
    }

    [HttpGet("auth/me")]
    [ProducesResponseType<ApiIdentityResponse>(StatusCodes.Status200OK)]
    public IActionResult Me() =>
        Ok(new ApiIdentityResponse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            User.Identity?.Name ?? "",
            User.FindFirstValue(ClaimTypes.Email) ?? "",
            User.FindFirstValue(ClaimTypes.Role) ?? ""));

    [HttpPost("auth/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<ApiUserResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers() =>
        Ok((await _users.GetAllAsync()).Select(ToUserResponse));

    [HttpGet("subjects")]
    [ProducesResponseType<IReadOnlyList<ApiSubjectResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjects() =>
        Ok((await _subjects.GetAllAsync()).Select(ToSubjectResponse));

    [HttpGet("departments")]
    [ProducesResponseType<IReadOnlyList<ApiDepartmentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments() =>
        Ok((await _departments.GetAllAsync()).Select(ToDepartmentResponse));

    [HttpGet("departments/{id}")]
    [ProducesResponseType<ApiDepartmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepartment(string id)
    {
        var department = await _departments.GetAsync(id);
        return department is null ? NotFound() : Ok(ToDepartmentResponse(department));
    }

    [HttpPost("departments")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiDepartmentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDepartment(ApiDepartmentRequest request)
    {
        var department = await _departments.CreateAsync(new DepartmentViewModel
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description
        });
        if (department is null)
            return Conflict(new ProblemDetails { Status = 409, Title = "Mã khoa đã tồn tại.", Instance = HttpContext.Request.Path });
        var response = ToDepartmentResponse(department);
        return CreatedAtAction(nameof(GetDepartment), new { id = response.Id }, response);
    }

    [HttpPut("departments/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiDepartmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDepartment(string id, ApiDepartmentRequest request)
    {
        if (await _departments.GetAsync(id) is null)
            return NotFound();
        var updated = await _departments.UpdateAsync(id, new DepartmentViewModel
        {
            Code = request.Code,
            Name = request.Name,
            Description = request.Description
        });
        if (!updated)
            return Conflict(new ProblemDetails { Status = 409, Title = "Mã khoa đã tồn tại hoặc không thể cập nhật.", Instance = HttpContext.Request.Path });
        return Ok(ToDepartmentResponse((await _departments.GetAsync(id))!));
    }

    [HttpDelete("departments/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDepartment(string id)
    {
        var result = await _departments.DeleteAsync(id);
        if (result.Success)
            return NoContent();
        return await _departments.GetAsync(id) is null
            ? NotFound()
            : Conflict(new ProblemDetails { Status = 409, Title = result.Error, Instance = HttpContext.Request.Path });
    }

    [HttpGet("subjects/{id}")]
    [ProducesResponseType<ApiSubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubject(string id)
    {
        var subject = await _subjects.GetAsync(id);
        return subject is null ? NotFound() : Ok(ToSubjectResponse(subject));
    }

    [HttpPost("subjects")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiSubjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateSubject(ApiSubjectRequest request)
    {
        if (await _departments.GetAsync(request.DepartmentId) is null)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Khoa không tồn tại hoặc mã khoa không hợp lệ.", Instance = HttpContext.Request.Path });
        var subject = await _subjects.CreateAsync(new SubjectViewModel
        {
            DepartmentId = request.DepartmentId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description
        });
        if (subject is null)
            return Conflict(new ProblemDetails { Status = 409, Title = "Mã môn học đã tồn tại.", Instance = HttpContext.Request.Path });
        var response = ToSubjectResponse(subject);
        return CreatedAtAction(nameof(GetSubject), new { id = response.Id }, response);
    }

    [HttpPut("subjects/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiSubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSubject(string id, ApiSubjectRequest request)
    {
        if (await _subjects.GetAsync(id) is null)
            return NotFound();
        if (await _departments.GetAsync(request.DepartmentId) is null)
            return BadRequest(new ProblemDetails { Status = 400, Title = "Khoa không tồn tại hoặc mã khoa không hợp lệ.", Instance = HttpContext.Request.Path });
        var updated = await _subjects.UpdateAsync(id, new SubjectViewModel
        {
            DepartmentId = request.DepartmentId,
            Code = request.Code,
            Name = request.Name,
            Description = request.Description
        });
        if (!updated)
            return Conflict(new ProblemDetails { Status = 409, Title = "Mã môn học đã tồn tại hoặc không thể cập nhật.", Instance = HttpContext.Request.Path });
        return Ok(ToSubjectResponse((await _subjects.GetAsync(id))!));
    }

    [HttpDelete("subjects/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSubject(string id)
    {
        var result = await _subjects.DeleteAsync(id);
        if (result.Success)
            return NoContent();
        return await _subjects.GetAsync(id) is null
            ? NotFound()
            : Conflict(new ProblemDetails { Status = 409, Title = result.Error, Instance = HttpContext.Request.Path });
    }

    [HttpGet("teachers/{id}/subjects")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<ApiSubjectResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeacherSubjects(string id)
    {
        var teacher = await _users.GetAsync(id);
        if (teacher is not { Role: Roles.Teacher })
            return NotFound();
        var ids = teacher.TeachingSubjectIds.ToArray();
        var assignedSubjects = ids.Length == 0
            ? new List<Subject>()
            : await _subjects.GetAllAsync();
        return Ok(assignedSubjects.Where(subject => ids.Contains(subject.Id))
            .Select(ToSubjectResponse));
    }

    [HttpPut("teachers/{id}/subjects")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetTeacherSubjects(string id, ApiTeacherSubjectsRequest request)
    {
        if (!await _users.AssignTeachingSubjectsAsync(id, request.SubjectIds))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Giảng viên không hợp lệ hoặc có môn học không tồn tại.",
                Instance = HttpContext.Request.Path
            });
        return NoContent();
    }

    [HttpPost("users")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiUserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(ApiCreateUserRequest request)
    {
        var created = await _users.CreateAsync(new CreateUserViewModel
        {
            FullName = request.FullName,
            Email = request.Email,
            Password = request.Password,
            Role = request.Role
        });

        if (!created)
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email đã tồn tại",
                Instance = HttpContext.Request.Path
            });

        var user = await _users.GetByEmailAsync(request.Email);
        if (user is null)
            throw new InvalidOperationException("Tài khoản vừa tạo không tìm thấy.");

        var response = ToUserResponse(user);
        return Created($"/api/users/{response.Id}", response);
    }

    [HttpPost("users/{id}/toggle-active")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUserActive(string id)
    {
        if (!ObjectId.TryParse(id, out _) || await _users.GetAsync(id) is not { } user)
            return NotFound();

        if (user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Không thể tự khóa tài khoản đang đăng nhập",
                Instance = HttpContext.Request.Path
            });

        await _users.ToggleAsync(id);
        user = await _users.GetAsync(id);
        return user is null ? NotFound() : Ok(ToUserResponse(user));
    }

    [HttpGet("classes")]
    [ProducesResponseType<IReadOnlyList<ApiClassResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetClasses()
    {
        var user = await GetCurrentUserAsync();
        return user is null
            ? Unauthorized()
            : Ok((await _classes.GetForUserAsync(user)).Select(ToClassResponse));
    }

    [HttpGet("classes/{id}")]
    [ProducesResponseType<ApiClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClass(string id)
    {
        var classroom = await _classes.GetAsync(id);
        if (classroom is null)
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(id, user))
            return Forbid();

        return Ok(ToClassResponse(classroom));
    }

    [HttpPost("classes")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiClassResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateClass(ApiCreateClassRequest request)
    {
        if (request.StudentIds is null ||
            !ObjectId.TryParse(request.TeacherId, out _) ||
            request.StudentIds.Any(id => !ObjectId.TryParse(id, out _)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Một hoặc nhiều mã tài khoản không hợp lệ",
                Instance = HttpContext.Request.Path
            });
        }

        var teacher = await _users.GetAsync(request.TeacherId);
        var subject = await _subjects.GetAsync(request.SubjectId);
        if (subject is null || !await _classes.CanTeachSubjectAsync(request.TeacherId, request.SubjectId))
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Môn học không tồn tại hoặc giảng viên chưa được phân công dạy môn này.",
                Instance = HttpContext.Request.Path
            });
        var studentIds = request.StudentIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var students = await Task.WhenAll(studentIds.Select(_users.GetAsync));
        if (teacher is not { IsActive: true, Role: Roles.Teacher } ||
            students.Any(student => student is not { IsActive: true, Role: Roles.Student }))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Lớp phải có giáo viên và học viên đang hoạt động với đúng vai trò",
                Instance = HttpContext.Request.Path
            });
        }

        var classroom = await _classes.CreateAsync(new CreateClassViewModel
        {
            Name = request.Name,
            Description = request.Description,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            StudentIds = studentIds.ToList()
        });
        var response = ToClassResponse(classroom);
        return CreatedAtAction(nameof(GetClass), new { id = response.Id }, response);
    }

    [HttpPut("classes/{id}/teaching-assignment")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTeacher(string id, ApiTeachingAssignmentRequest request)
    {
        var classroom = await _classes.GetAsync(id);
        if (classroom is null)
            return NotFound();
        if (!await _classes.AssignTeacherAsync(id, request.TeacherId))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Giảng viên không hợp lệ hoặc không hoạt động",
                Instance = HttpContext.Request.Path
            });
        }

        classroom = await _classes.GetAsync(id);
        return classroom is null ? NotFound() : Ok(ToClassResponse(classroom));
    }

    [HttpPut("classes/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiClassResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateClass(string id, ApiCreateClassRequest request)
    {
        if (await _classes.GetAsync(id) is null)
            return NotFound();
        if (request.StudentIds is null ||
            !ObjectId.TryParse(request.TeacherId, out _) ||
            request.StudentIds.Any(studentId => !ObjectId.TryParse(studentId, out _)))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Một hoặc nhiều mã tài khoản không hợp lệ",
                Instance = HttpContext.Request.Path
            });
        }

        var updated = await _classes.UpdateAsync(id, new UpdateClassViewModel
        {
            Name = request.Name,
            Description = request.Description,
            SubjectId = request.SubjectId,
            TeacherId = request.TeacherId,
            StudentIds = request.StudentIds.ToList()
        });
        if (!updated)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Giáo viên hoặc học viên phải đang hoạt động và có đúng vai trò",
                Instance = HttpContext.Request.Path
            });
        }

        var classroom = await _classes.GetAsync(id);
        return classroom is null ? NotFound() : Ok(ToClassResponse(classroom));
    }

    [HttpDelete("classes/{id}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveClass(string id)
    {
        var result = await _classes.ArchiveAsync(id);
        if (result.Success)
            return NoContent();
        if (await _classes.GetAsync(id) is null)
            return NotFound();
        return Conflict(new ProblemDetails { Status = 409, Title = result.Error, Instance = HttpContext.Request.Path });
    }

    [HttpGet("classes/{classId}/lessons")]
    [ProducesResponseType<IReadOnlyList<ApiLessonResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLessons(string classId)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId))
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(classId, user))
            return Forbid();

        var lessons = await _lessons.GetByClassAsync(classObjectId, user.Role == Roles.Student);
        return Ok(lessons.Select(ToLessonResponse));
    }

    [HttpGet("lessons/{id}")]
    [ProducesResponseType<ApiLessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLesson(string id)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null)
            return NotFound();

        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(lesson.ClassId.ToString(), user) ||
            (user.Role == Roles.Student && (!lesson.IsPublished || lesson.IsArchived)))
        {
            return Forbid();
        }

        return Ok(ToLessonResponse(lesson));
    }

    [HttpPost("classes/{classId}/lessons")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<ApiLessonResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLesson(string classId, ApiLessonRequest request)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId))
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(classId, user))
            return Forbid();

        var lesson = await _lessons.CreateAsync(classObjectId, new LessonViewModel
        {
            Title = request.Title,
            Content = request.Content,
            IsPublished = request.IsPublished
        });
        var response = ToLessonResponse(lesson);
        return CreatedAtAction(nameof(GetLesson), new { id = response.Id }, response);
    }

    [HttpPut("lessons/{id}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<ApiLessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateLesson(string id, ApiLessonRequest request)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null || lesson.IsArchived)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(lesson.ClassId.ToString(), user))
            return Forbid();

        await _lessons.UpdateAsync(id, new LessonViewModel
        {
            Title = request.Title,
            Content = request.Content,
            IsPublished = request.IsPublished
        });
        lesson = await _lessons.GetAsync(id);
        return lesson is null ? NotFound() : Ok(ToLessonResponse(lesson));
    }

    [HttpDelete("lessons/{id}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveLesson(string id)
    {
        var lesson = await _lessons.GetAsync(id);
        if (lesson is null || lesson.IsArchived)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(lesson.ClassId.ToString(), user))
            return Forbid();
        return await _lessons.ArchiveAsync(id) ? NoContent() : NotFound();
    }

    [HttpGet("classes/{classId}/assignments")]
    [ProducesResponseType<IReadOnlyList<ApiAssignmentResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(string classId)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId))
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(classId, user))
            return Forbid();

        var assignments = await _assignments.GetByClassAsync(classObjectId, user.Role == Roles.Student);
        return Ok(assignments.Select(ToAssignmentResponse));
    }

    [HttpGet("assignments/{id}")]
    [ProducesResponseType<ApiAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignment(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(assignment.ClassId.ToString(), user) ||
            (user.Role == Roles.Student && (!assignment.IsPublished || assignment.IsArchived)))
        {
            return Forbid();
        }

        return Ok(ToAssignmentResponse(assignment));
    }

    [HttpPost("classes/{classId}/assignments")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<ApiAssignmentResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAssignment(string classId, ApiAssignmentRequest request)
    {
        if (!ObjectId.TryParse(classId, out var classObjectId))
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(classId, user))
            return Forbid();

        var assignment = await _assignments.CreateAsync(classObjectId, new AssignmentViewModel
        {
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate.UtcDateTime,
            IsPublished = request.IsPublished
        });
        var response = ToAssignmentResponse(assignment);
        return CreatedAtAction(nameof(GetAssignment), new { id = response.Id }, response);
    }

    [HttpPut("assignments/{id}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<ApiAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAssignment(string id, ApiAssignmentRequest request)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();

        await _assignments.UpdateAsync(id, new AssignmentViewModel
        {
            Title = request.Title,
            Description = request.Description,
            DueDate = request.DueDate.UtcDateTime,
            IsPublished = request.IsPublished
        });
        assignment = await _assignments.GetAsync(id);
        return assignment is null ? NotFound() : Ok(ToAssignmentResponse(assignment));
    }

    [HttpDelete("assignments/{id}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ArchiveAssignment(string id)
    {
        var assignment = await _assignments.GetAsync(id);
        if (assignment is null || assignment.IsArchived)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();
        return await _assignments.ArchiveAsync(id) ? NoContent() : NotFound();
    }

    [HttpGet("assignments/{assignmentId}/submissions")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<IReadOnlyList<ApiSubmissionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubmissions(string assignmentId)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        if (assignment is null)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();

        return Ok((await _submissions.GetByAssignmentAsync(assignment.Id)).Select(ToSubmissionResponse));
    }

    [HttpGet("assignments/{assignmentId}/submission")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<ApiSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySubmission(string assignmentId)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        if (assignment is null)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (assignment.IsArchived || !assignment.IsPublished || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();

        var submission = await _submissions.GetMineAsync(assignment.Id, user.Id);
        return submission is null ? NotFound() : Ok(ToSubmissionResponse(submission));
    }

    [HttpPut("assignments/{assignmentId}/submission")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType<ApiSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitWork(string assignmentId, ApiSubmissionRequest request)
    {
        var assignment = await _assignments.GetAsync(assignmentId);
        if (assignment is null)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (assignment.IsArchived || !assignment.IsPublished || !await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();
        if (assignment.DueDate <= DateTime.UtcNow)
            return Conflict(new ProblemDetails { Status = 409, Title = "Hạn nộp bài đã qua.", Instance = HttpContext.Request.Path });

        var submission = await _submissions.SubmitAsync(
            assignment.Id,
            user.Id,
            new SubmissionViewModel { Content = request.Content });
        return Ok(ToSubmissionResponse(submission));
    }

    [HttpPut("submissions/{id}/grade")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType<ApiSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GradeSubmission(string id, ApiGradeRequest request)
    {
        var submission = await _submissions.GetAsync(id);
        if (submission is null)
            return NotFound();
        var assignment = await _assignments.GetAsync(submission.AssignmentId.ToString());
        if (assignment is null)
            return NotFound();
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (!await _classes.HasAccessAsync(assignment.ClassId.ToString(), user))
            return Forbid();

        submission = await _submissions.GradeAsync(id, new GradeViewModel
        {
            Grade = request.Grade,
            TeacherComment = request.TeacherComment
        });
        return submission is null ? NotFound() : Ok(ToSubmissionResponse(submission));
    }

    [HttpGet("activity-logs")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<IReadOnlyList<ApiActivityLogResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivityLogs() =>
        Ok((await _activityLogs.GetAllAsync()).Select(log => new ApiActivityLogResponse(
            log.Id.ToString(),
            log.Email,
            log.Action,
            log.Success,
            log.IpAddress,
            log.Message,
            AsUtcOffset(log.CreatedAt))));

    [HttpGet("schedule/availabilities")]
    [ProducesResponseType<IReadOnlyList<ApiTeacherAvailabilityResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailabilities()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        if (user.Role is not (Roles.Admin or Roles.Teacher))
            return Forbid();
        var requests = await _schedules.GetAvailabilitiesAsync(user.Role == Roles.Admin ? null : user.Id);
        return Ok(requests.Select(request => new ApiTeacherAvailabilityResponse(
            request.Id.ToString(),
            request.TeacherId.ToString(),
            AsUtcOffset(request.StartAt),
            AsUtcOffset(request.EndAt),
            request.Note,
            request.Status)));
    }

    [HttpPost("schedule/availabilities")]
    [Authorize(Roles = Roles.Teacher)]
    [ProducesResponseType<ApiTeacherAvailabilityResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddAvailability(ApiTeacherAvailabilityRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        var result = await _schedules.AddAvailabilityAsync(user.Id, new TeacherAvailabilityViewModel
        {
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            Note = request.Note
        });
        if (result.Availability is null)
            return BadRequest(new ProblemDetails { Status = 400, Title = result.Error, Instance = HttpContext.Request.Path });
        var availability = result.Availability;
        var response = new ApiTeacherAvailabilityResponse(
            availability.Id.ToString(),
            availability.TeacherId.ToString(),
            AsUtcOffset(availability.StartAt),
            AsUtcOffset(availability.EndAt),
            availability.Note,
            availability.Status);
        return Created($"/api/schedule/availabilities/{response.Id}", response);
    }

    [HttpPut("schedule/availabilities/{id}/review")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewAvailability(string id, ApiAvailabilityReviewRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        return await _schedules.ReviewAvailabilityAsync(id, user.Id, request.Approve)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("schedule/sessions")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<ApiSessionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateSession(ApiCreateSessionRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        var result = await _schedules.CreateSessionAsync(
            request.ClassId,
            request.SubjectId,
            request.StartAt,
            request.EndAt,
            request.Capacity,
            user.Id);
        if (result.Session is null)
            return BadRequest(new ProblemDetails { Status = 400, Title = result.Error, Instance = HttpContext.Request.Path });
        var view = (await _schedules.GetSessionsAsync(user)).Single(x => x.Id == result.Session.Id.ToString());
        var response = ToSessionResponse(view);
        return Created($"/api/schedule/sessions/{response.Id}", response);
    }

    [HttpGet("schedule/sessions")]
    [ProducesResponseType<IReadOnlyList<ApiSessionResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions()
    {
        var user = await GetCurrentUserAsync();
        return user is null
            ? Unauthorized()
            : Ok((await _schedules.GetSessionsAsync(user)).Select(ToSessionResponse));
    }

    [HttpGet("schedule/sessions/{id}")]
    [ProducesResponseType<ApiSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(string id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        var details = await _schedules.GetSessionDetailsAsync(id, user);
        return details is null ? NotFound() : Ok(ToSessionResponse(details.Session));
    }

    [HttpPost("schedule/sessions/{id}/enrollment")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EnrollInSession(string id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        var result = await _schedules.EnrollAsync(id, user);
        return result.Success
            ? NoContent()
            : Conflict(new ProblemDetails { Status = 409, Title = result.Error, Instance = HttpContext.Request.Path });
    }

    [HttpDelete("schedule/sessions/{id}/enrollment")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawFromSession(string id)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
            return Unauthorized();
        var result = await _schedules.CancelEnrollmentAsync(id, user);
        return result.Success ? NoContent() : NotFound();
    }

    [HttpDelete("schedule/sessions/{id}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Teacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelSession(string id, ApiCancelSessionRequest request)
    {
        var user = await GetCurrentUserAsync();
        var session = await _schedules.GetSessionAsync(id);
        if (user is null)
            return Unauthorized();
        if (session is null)
            return NotFound();
        if (user.Role == Roles.Teacher && session.TeacherId != user.Id)
            return Forbid();
        return await _schedules.CancelSessionAsync(id, request.Reason)
            ? NoContent()
            : BadRequest(new ProblemDetails { Status = 400, Title = "Buổi học không thể hủy.", Instance = HttpContext.Request.Path });
    }

    private Task<User?> GetCurrentUserAsync() =>
        _users.GetAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "");

    private static ApiIdentityResponse ToIdentityResponse(User user) =>
        new(user.Id.ToString(), user.FullName, user.Email, user.Role);

    private static ApiUserResponse ToUserResponse(User user) =>
        new(user.Id.ToString(), user.FullName, user.Email, user.Role, user.IsActive, AsUtcOffset(user.CreatedAt));

    private static ApiClassResponse ToClassResponse(ClassRoom classroom) =>
        new(classroom.Id.ToString(), classroom.Name, classroom.Description, classroom.SubjectId.ToString(), classroom.TeacherId.ToString(),
            classroom.StudentIds.Select(id => id.ToString()).ToArray(), AsUtcOffset(classroom.CreatedAt));

    private static ApiSubjectResponse ToSubjectResponse(Subject subject) =>
        new(subject.Id.ToString(), subject.DepartmentId.ToString(), subject.Code, subject.Name, subject.Description, AsUtcOffset(subject.CreatedAt));

    private static ApiDepartmentResponse ToDepartmentResponse(Department department) =>
        new(department.Id.ToString(), department.Code, department.Name, department.Description, AsUtcOffset(department.CreatedAt));

    private static ApiLessonResponse ToLessonResponse(Lesson lesson) =>
        new(lesson.Id.ToString(), lesson.ClassId.ToString(), lesson.Title, lesson.Content, lesson.IsPublished, AsUtcOffset(lesson.CreatedAt));

    private static ApiAssignmentResponse ToAssignmentResponse(Assignment assignment) =>
        new(assignment.Id.ToString(), assignment.ClassId.ToString(), assignment.Title, assignment.Description,
            AsUtcOffset(assignment.DueDate), assignment.IsPublished, AsUtcOffset(assignment.CreatedAt));

    private static ApiSubmissionResponse ToSubmissionResponse(Submission submission) =>
        new(submission.Id.ToString(), submission.AssignmentId.ToString(), submission.StudentId.ToString(),
            submission.Content, submission.Grade, submission.TeacherComment, AsUtcOffset(submission.SubmittedAt),
            submission.GradedAt.HasValue ? AsUtcOffset(submission.GradedAt.Value) : null);

    private static DateTimeOffset AsUtcOffset(DateTime dateTime) =>
        new(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));

    private static ApiSessionResponse ToSessionResponse(SessionListItem session) =>
        new(session.Id, session.ClassId, session.SubjectId, session.SubjectName, session.ClassName, session.TeacherName, session.StartAt, session.EndAt,
            session.Capacity, session.EnrolledCount, session.IsEnrolled, session.Status, session.CancellationReason);
}
