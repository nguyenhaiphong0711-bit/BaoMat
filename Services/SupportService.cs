using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
            ticket.Messages.Select(x => new SupportMessageResponse(x.Id, x.SenderName, x.SenderRole, x.Content, x.SentAt)).ToArray(),
            ticket.History.Select(x => new SupportHistoryResponse(x.Action, x.FromStatus, x.ToStatus,
                x.ActorName, x.ActorRole, x.CreatedAt)).ToArray());
    }

    public async Task<object> ListAsync(User user, string? query, string? status, int page)
    {
        // Read documents individually so one legacy/manual Atlas document with a
        // wrong BSON type cannot make the complete Admin support queue return 500.
        // Invalid scope identifiers are deliberately skipped instead of exposed.
        BsonDocument rawFilter;
        if (user.Role == Roles.Admin) rawFilter = new();
        else if (user.Role == Roles.Student)
            rawFilter = new("$or", new BsonArray {
                new BsonDocument("StudentId", user.Id), new BsonDocument("StudentId", user.Id.ToString()) });
        else if (user.Role == Roles.Staff && user.DepartmentId.HasValue)
            rawFilter = new("$or", new BsonArray {
                new BsonDocument("DepartmentId", user.DepartmentId.Value),
                new BsonDocument("DepartmentId", user.DepartmentId.Value.ToString()) });
        else rawFilter = new("_id", new BsonDocument("$exists", false));
        var documents = await db.RawSupportTickets.Find(rawFilter)
            .Sort(new BsonDocument("UpdatedAt", -1)).ToListAsync();
        var visible = new List<SupportTicketResponse>();
        foreach (var document in documents)
        {
            SupportTicket ticket;
            try
            {
                NormalizeObjectId(document, "StudentId");
                NormalizeObjectId(document, "DepartmentId");
                NormalizeObjectId(document, "AssignedStaffId");
                NormalizeNestedObjectIds(document, "Messages", "SenderId");
                NormalizeNestedObjectIds(document, "History", "ActorId");
                ticket = BsonSerializer.Deserialize<SupportTicket>(document);
            }
            catch (FormatException) { continue; }
            catch (MongoDB.Bson.Serialization.BsonSerializationException) { continue; }
            ticket.Messages ??= new();
            ticket.History ??= new();
            var response = await ResponseAsync(user, ticket);
            if (response is null || (!string.IsNullOrWhiteSpace(query) &&
                !(response.Subject + " " + response.StudentName).Contains(query.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(status) && response.Status != status)) continue;
            visible.Add(response with { Messages = Array.Empty<SupportMessageResponse>(),
                History = Array.Empty<SupportHistoryResponse>() });
        }
        return new { items = visible.Skip((page - 1) * 15).Take(15), total = visible.Count, page, pageSize = 15 };
    }

    private static void NormalizeObjectId(BsonDocument document, string field)
    {
        if (document.TryGetValue(field, out var value) && value.IsString &&
            ObjectId.TryParse(value.AsString, out var parsed))
            document[field] = parsed;
    }

    private static void NormalizeNestedObjectIds(BsonDocument document, string arrayField, string idField)
    {
        if (!document.TryGetValue(arrayField, out var value) || !value.IsBsonArray) return;
        foreach (var item in value.AsBsonArray.Where(x => x.IsBsonDocument))
            NormalizeObjectId(item.AsBsonDocument, idField);
    }

    public async Task<SupportTicket> CreateAsync(User student, SupportCreateRequest request)
    {
        if (student.Role != Roles.Student || student.DepartmentId is not { } departmentId ||
            !await db.Departments.Find(x => x.Id == departmentId && !x.IsArchived).AnyAsync())
            throw new ArgumentException("Tài khoản sinh viên chưa được gắn với khoa đang hoạt động.");
        var ticket = new SupportTicket { StudentId = student.Id, DepartmentId = departmentId,
            Subject = request.Subject.Trim(), Category = request.Category.Trim(), Priority = request.Priority };
        ticket.Messages.Add(Message(student, request.Content));
        ticket.History.Add(History("Created", "", TicketStatuses.Open, student));
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
        var oldStatus = ticket.Status; var oldAssigned = ticket.AssignedStaffId;
        ticket.Messages.Add(Message(user, request.Content));
        if (manager && ticket.AssignedStaffId is null && user.Role == Roles.Staff)
            ticket.AssignedStaffId = user.Id;
        if (manager && ticket.Status == TicketStatuses.Open) ticket.Status = TicketStatuses.InProgress;
        if (user.Role == Roles.Student && ticket.Status == TicketStatuses.Resolved)
        { ticket.Status = TicketStatuses.Open; ticket.ResolvedAt = null; }
        ticket.UpdatedAt = DateTime.UtcNow;
        if (oldStatus != ticket.Status)
            ticket.History.Add(History("StatusChanged", oldStatus, ticket.Status, user));
        if (oldAssigned != ticket.AssignedStaffId)
            ticket.History.Add(History("Assigned", ticket.Status, ticket.Status, user));
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
        var oldStatus = ticket.Status; var oldAssigned = ticket.AssignedStaffId;
        ticket.AssignedStaffId = assigned; ticket.Status = request.Status;
        ticket.ResolvedAt = request.Status == TicketStatuses.Resolved ? DateTime.UtcNow : null;
        ticket.UpdatedAt = DateTime.UtcNow;
        if (oldStatus != ticket.Status)
            ticket.History.Add(History("StatusChanged", oldStatus, ticket.Status, user));
        if (oldAssigned != ticket.AssignedStaffId)
            ticket.History.Add(History("Assigned", ticket.Status, ticket.Status, user));
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

    private static SupportHistoryEntry History(string action, string from, string to, User user) => new()
    { Action = action, FromStatus = from, ToStatus = to, ActorId = user.Id,
        ActorName = user.FullName, ActorRole = user.Role };

    private async Task<bool> SaveAsync(SupportTicket ticket, int expected)
    {
        if (ticket.Revision != expected) return false; ticket.Revision++;
        return (await db.SupportTickets.ReplaceOneAsync(x => x.Id == ticket.Id && x.Revision == expected, ticket)).MatchedCount == 1;
    }
}
