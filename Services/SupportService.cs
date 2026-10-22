using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

public sealed class SupportService(MongoContext db, UserService users, LearningEventService events,
    NotificationService notifications)
{
    public async Task<SupportTicket?> GetAsync(string id) => ObjectId.TryParse(id, out var key)
        ? await db.SupportTickets.Find(x => x.Id == key).FirstOrDefaultAsync() : null;

    public async Task<object[]> EligibleStaffAsync(User user, SupportTicket ticket)
    {
        if (!await CanManageAsync(user, ticket)) throw new UnauthorizedAccessException();
        return (await db.Users.Find(x => x.Role == Roles.Staff && x.IsActive &&
            x.DepartmentId == ticket.DepartmentId).SortBy(x => x.FullName).ToListAsync())
            .Select(x => (object)new { id = x.Id.ToString(), x.FullName }).ToArray();
    }

    private async Task<bool> CanManageAsync(User user, SupportTicket ticket)
    {
        if (user.Role == Roles.Admin) return true;
        return user.Role == Roles.Staff && user.DepartmentId == ticket.DepartmentId &&
            await users.HasPermissionAsync(user.Id, PermissionCodes.SupportManage) &&
            await db.Departments.Find(x => x.Id == ticket.DepartmentId && !x.IsArchived).AnyAsync();
    }

    private async Task<bool> CanReadAsync(User user, SupportTicket ticket) =>
        (user.Role == Roles.Student && ticket.StudentId == user.Id) || user.Role == Roles.Admin ||
        (user.Role == Roles.Staff && user.DepartmentId == ticket.DepartmentId &&
         await db.Departments.Find(x => x.Id == ticket.DepartmentId && !x.IsArchived).AnyAsync());

    public async Task<SupportTicketResponse?> ResponseAsync(User user, SupportTicket ticket)
    {
        if (!await CanReadAsync(user, ticket)) return null;
        var student = await db.Users.Find(x => x.Id == ticket.StudentId).FirstOrDefaultAsync();
        var department = await db.Departments.Find(x => x.Id == ticket.DepartmentId).FirstOrDefaultAsync();
        var assigned = ticket.AssignedStaffId.HasValue
            ? await db.Users.Find(x => x.Id == ticket.AssignedStaffId.Value).FirstOrDefaultAsync() : null;
        var manager = await CanManageAsync(user, ticket);
        return new(ticket.Id.ToString(), ticket.Revision, ticket.Subject, ticket.Category, ticket.Priority,
            ticket.Status, student?.FullName ?? "", department?.Name ?? "", assigned?.FullName ?? "",
            ticket.AssignedStaffId?.ToString(), ticket.CreatedAt, ticket.UpdatedAt,
            ticket.Status != TicketStatuses.Closed && (manager || user.Id == ticket.StudentId), manager,
            ticket.Messages.Select(x => new SupportMessageResponse(x.Id, x.SenderName, x.SenderRole, x.Content, x.SentAt)).ToArray());
    }

    public async Task<object> ListAsync(User user, string? query, string? status, int page)
    {
        FilterDefinition<SupportTicket> filter;
        if (user.Role == Roles.Admin) filter = Builders<SupportTicket>.Filter.Empty;
        else if (user.Role == Roles.Student) filter = Builders<SupportTicket>.Filter.Eq(x => x.StudentId, user.Id);
        else if (user.Role == Roles.Staff && user.DepartmentId.HasValue &&
                 await db.Departments.Find(x => x.Id == user.DepartmentId && !x.IsArchived).AnyAsync())
            filter = Builders<SupportTicket>.Filter.Eq(x => x.DepartmentId, user.DepartmentId.Value);
        else filter = Builders<SupportTicket>.Filter.Where(_ => false);
        var visible = new List<SupportTicketResponse>();
        foreach (var ticket in await db.SupportTickets.Find(filter).SortByDescending(x => x.UpdatedAt).ToListAsync())
        {
            var response = await ResponseAsync(user, ticket);
            if (response is null || (!string.IsNullOrWhiteSpace(query) &&
                !(response.Subject + " " + response.StudentName).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(status) && response.Status != status)) continue;
            visible.Add(response with { Messages = Array.Empty<SupportMessageResponse>() });
        }
        return new { items = visible.Skip((page - 1) * 15).Take(15), total = visible.Count, page, pageSize = 15 };
    }

    public async Task<SupportTicket> CreateAsync(User student, SupportCreateRequest request)
    {
        if (student.Role != Roles.Student || student.DepartmentId is not { } departmentId ||
            !await db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).AnyAsync())
            throw new ArgumentException("Tài khoản sinh viên chưa được gắn với khoa đang hoạt động.");
        var ticket = new SupportTicket { StudentId = student.Id, DepartmentId = departmentId,
            Subject = request.Subject.Trim(), Category = request.Category.Trim(), Priority = request.Priority };
        ticket.Messages.Add(Message(student, request.Content));
        await db.SupportTickets.InsertOneAsync(ticket);
        await events.RecordAsync("support.created", student, "SupportTicket", ticket.Id,
            "Đã tạo yêu cầu hỗ trợ", student.Id, departmentId);
        await notifications.NotifyDepartmentAsync(departmentId, "support.created", "Yêu cầu hỗ trợ mới",
            ticket.Subject, $"/app/support/{ticket.Id}", student.Id);
        return ticket;
    }

    public async Task<bool> AddMessageAsync(User user, SupportTicket ticket, SupportMessageRequest request)
    {
        if (!await CanReadAsync(user, ticket)) throw new UnauthorizedAccessException();
        if (ticket.Status == TicketStatuses.Closed || ticket.Messages.Count >= 200)
            throw new ArgumentException("Yêu cầu đã đóng hoặc đã đạt giới hạn 200 tin nhắn.");
        var manager = await CanManageAsync(user, ticket);
        if (user.Role != Roles.Student && !manager) throw new UnauthorizedAccessException();
        ticket.Messages.Add(Message(user, request.Content));
        if (manager && ticket.AssignedStaffId is null && user.Role == Roles.Staff)
            ticket.AssignedStaffId = user.Id;
        if (manager && ticket.Status == TicketStatuses.Open) ticket.Status = TicketStatuses.InProgress;
        if (user.Role == Roles.Student && ticket.Status == TicketStatuses.Resolved)
        { ticket.Status = TicketStatuses.Open; ticket.ResolvedAt = null; }
        ticket.UpdatedAt = DateTime.UtcNow;
        var saved = await SaveAsync(ticket, request.Revision);
        if (saved)
        {
            await events.RecordAsync("support.replied", user, "SupportTicket", ticket.Id,
                "Đã cập nhật hội thoại hỗ trợ", ticket.StudentId, ticket.DepartmentId);
            if (user.Role == Roles.Student)
            {
                if (ticket.AssignedStaffId.HasValue) await notifications.AddAsync(ticket.AssignedStaffId.Value,
                    "support.replied", "Sinh viên vừa trả lời", ticket.Subject, $"/app/support/{ticket.Id}");
                else await notifications.NotifyDepartmentAsync(ticket.DepartmentId, "support.replied",
                    "Sinh viên vừa trả lời", ticket.Subject, $"/app/support/{ticket.Id}", user.Id);
            }
            else await notifications.AddAsync(ticket.StudentId, "support.replied", "Yêu cầu hỗ trợ có phản hồi mới",
                ticket.Subject, $"/app/support/{ticket.Id}");
        }
        return saved;
    }

    public async Task<bool> ManageAsync(User user, SupportTicket ticket, SupportManageRequest request)
    {
        if (!await CanManageAsync(user, ticket)) throw new UnauthorizedAccessException();
        ObjectId? assigned = null;
        if (!string.IsNullOrWhiteSpace(request.AssignedStaffId))
        {
            if (!ObjectId.TryParse(request.AssignedStaffId, out var staffId)) throw new ArgumentException("Cán bộ xử lý không hợp lệ.");
            if (user.Role == Roles.Staff && staffId != user.Id) throw new UnauthorizedAccessException();
            var staff = await db.Users.Find(x => x.Id == staffId && x.Role == Roles.Staff && x.IsActive &&
                x.DepartmentId == ticket.DepartmentId).FirstOrDefaultAsync();
            if (staff is null) throw new ArgumentException("Cán bộ xử lý phải đang hoạt động và thuộc đúng khoa.");
            assigned = staffId;
        }
        else if (user.Role == Roles.Staff && request.Status is TicketStatuses.InProgress or TicketStatuses.Resolved)
            assigned = user.Id;
        if (request.Status is TicketStatuses.InProgress or TicketStatuses.Resolved && assigned is null)
            throw new ArgumentException("Cần phân công cán bộ trước khi xử lý hoặc hoàn tất.");
        ticket.AssignedStaffId = assigned; ticket.Status = request.Status;
        ticket.ResolvedAt = request.Status == TicketStatuses.Resolved ? DateTime.UtcNow : null;
        ticket.UpdatedAt = DateTime.UtcNow;
        var saved = await SaveAsync(ticket, request.Revision);
        if (saved)
        {
            await events.RecordAsync("support.status", user, "SupportTicket", ticket.Id,
                "Yêu cầu hỗ trợ chuyển sang " + request.Status, ticket.StudentId, ticket.DepartmentId);
            await notifications.AddAsync(ticket.StudentId, "support.status", "Trạng thái hỗ trợ đã thay đổi",
                $"{ticket.Subject}: {request.Status}", $"/app/support/{ticket.Id}");
        }
        return saved;
    }

    private static SupportMessage Message(User user, string content) => new()
    { SenderId = user.Id, SenderName = user.FullName, SenderRole = user.Role, Content = content.Trim() };

    private async Task<bool> SaveAsync(SupportTicket ticket, int expected)
    {
        if (ticket.Revision != expected) return false; ticket.Revision++;
        return (await db.SupportTickets.ReplaceOneAsync(x => x.Id == ticket.Id && x.Revision == expected, ticket)).MatchedCount == 1;
    }
}
