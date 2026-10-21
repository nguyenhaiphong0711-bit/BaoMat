using LMS.Data;
using LMS.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LMS.Services;

// Illustrative economics/business data; this is not an official NEU catalog.
public static class DemoDataSeeder
{
    private const int Count = 50;
    private const string Password = "Demo@123";
    private static readonly string[] Departments = ["Kinh tế học", "Quản trị kinh doanh", "Kế toán - Kiểm toán", "Ngân hàng - Tài chính", "Thương mại và Kinh tế quốc tế", "Công nghệ thông tin", "Marketing", "Luật", "Du lịch và Khách sạn", "Bất động sản và Kinh tế tài nguyên", "Khoa học quản lý", "Thống kê", "Bảo hiểm", "Môi trường và Đô thị", "Ngoại ngữ kinh tế"];
    private static readonly string[] Programs = ["Kinh tế học", "Kinh tế phát triển", "Kinh tế quốc tế", "Quản trị kinh doanh", "Quản trị doanh nghiệp", "Kế toán", "Kiểm toán", "Tài chính doanh nghiệp", "Ngân hàng", "Thương mại điện tử", "Kinh doanh quốc tế", "Marketing", "Truyền thông Marketing", "Hệ thống thông tin quản lý", "Khoa học dữ liệu trong kinh tế", "Công nghệ thông tin", "Luật kinh tế", "Quản trị khách sạn", "Quản trị du lịch và lữ hành", "Bất động sản", "Quản lý công", "Thống kê kinh tế", "Bảo hiểm", "Kinh tế tài nguyên", "Ngôn ngữ Anh kinh tế"];
    private static readonly string[] Subjects = ["Kinh tế vi mô", "Kinh tế vĩ mô", "Toán cao cấp", "Xác suất và thống kê", "Nguyên lý kế toán", "Kế toán tài chính", "Kiểm toán căn bản", "Tài chính doanh nghiệp", "Tiền tệ và ngân hàng", "Thị trường chứng khoán", "Quản trị học", "Quản trị chiến lược", "Quản trị nhân lực", "Quản trị tác nghiệp", "Marketing căn bản", "Nghiên cứu Marketing", "Hành vi người tiêu dùng", "Kinh doanh quốc tế", "Thanh toán quốc tế", "Logistics và chuỗi cung ứng", "Thương mại điện tử", "Kinh tế lượng", "Phân tích dữ liệu kinh doanh", "Cơ sở dữ liệu", "Lập trình ứng dụng", "Hệ thống thông tin quản lý", "Chuyển đổi số", "Luật kinh tế", "Pháp luật đại cương", "Quản lý nhà nước về kinh tế", "Kinh tế công cộng", "Kinh tế phát triển", "Kinh tế môi trường", "Kinh tế đô thị", "Quản trị khách sạn", "Quản trị lữ hành", "Marketing du lịch", "Định giá bất động sản", "Đầu tư bất động sản", "Thống kê kinh doanh", "Dự báo kinh tế", "Quản trị rủi ro", "Nguyên lý bảo hiểm", "Tài chính công", "Đầu tư tài chính", "Khởi sự kinh doanh", "Đạo đức kinh doanh", "Giao tiếp kinh doanh", "Tiếng Anh thương mại", "Phương pháp nghiên cứu kinh tế"];
    private static readonly string[] Students = ["Nguyễn Hà Phương", "Trần Minh Anh", "Lê Thu Hà", "Phạm Đức Anh", "Hoàng Ngọc Mai", "Vũ Quang Huy", "Đỗ Khánh Linh", "Bùi Gia Bảo", "Đặng Phương Thảo", "Ngô Tuấn Kiệt", "Dương Hoài An", "Đinh Nhật Minh", "Mai Thanh Hương", "Đoàn Quốc Việt", "Trịnh Bảo Ngọc", "Nguyễn Thùy Dương", "Trần Anh Tú", "Lê Minh Châu", "Phạm Quỳnh Anh", "Hoàng Đức Long", "Vũ Hải Yến", "Đỗ Thành Đạt", "Bùi Ngọc Anh", "Đặng Minh Khang", "Ngô Hà My", "Dương Trung Kiên", "Đinh Phương Linh", "Mai Đức Thắng", "Đoàn Thu Trang", "Trịnh Gia Hân", "Nguyễn Quốc Bảo", "Trần Khánh Vy", "Lê Hoàng Nam", "Phạm Mai Chi", "Hoàng Anh Dũng", "Vũ Thanh Tâm", "Đỗ Quang Minh", "Bùi Thu Phương", "Đặng Việt Anh", "Ngô Bảo Trâm", "Dương Minh Đức", "Đinh Ngọc Hà", "Mai Quốc Khánh", "Đoàn Hải Anh", "Trịnh Thu Huyền", "Nguyễn Thành Công", "Trần Ngọc Lan", "Lê Anh Khoa", "Phạm Thảo Nguyên", "Hoàng Minh Triết"];

    public static async Task InitializeAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        if (!env.IsDevelopment() || !config.GetValue<bool>("DemoData:Enabled")) return;
        var db = scope.ServiceProvider.GetRequiredService<MongoContext>(); var now = DateTime.UtcNow;

        var departments = new List<Department>();
        for (var i = 0; i < Departments.Length; i++) departments.Add(await GetOrCreate(db.Departments,
            x => x.Code == $"DEMO-D{i + 1:D2}", new Department { Code = $"DEMO-D{i + 1:D2}", Name = $"Khoa/Viện {Departments[i]}", Description = "Dữ liệu minh họa tham khảo bối cảnh đào tạo kinh tế; không phải danh mục chính thức của NEU." }));
        var programs = new List<AcademicProgram>();
        for (var i = 0; i < Programs.Length; i++) programs.Add(await GetOrCreate(db.AcademicPrograms,
            x => x.Code == $"DEMO-P{i + 1:D2}", new AcademicProgram { DepartmentId = departments[i % departments.Count].Id, Code = $"DEMO-P{i + 1:D2}", Name = Programs[i], Description = "Chương trình minh họa phục vụ kiểm thử LMS." }));
        var subjects = new List<Subject>();
        for (var i = 0; i < Count; i++)
        {
            var subject = await GetOrCreate(db.Subjects, x => x.Code == $"DEMO-HP{i + 1:D3}", new Subject { DepartmentId = departments[i % departments.Count].Id, Code = $"DEMO-HP{i + 1:D3}", Name = Subjects[i], Description = "Học phần minh họa 3 tín chỉ." });
            subjects.Add(subject); var program = programs[i % programs.Count];
            await db.ProgramCourses.UpdateOneAsync(x => x.ProgramId == program.Id && x.SubjectId == subject.Id,
                Builders<ProgramCourse>.Update.SetOnInsert(x => x.ProgramId, program.Id).SetOnInsert(x => x.SubjectId, subject.Id).SetOnInsert(x => x.SemesterNumber, i % 8 + 1).SetOnInsert(x => x.Credits, 3).SetOnInsert(x => x.IsRequired, i % 4 != 0), new UpdateOptions { IsUpsert = true });
        }
        var term = await GetOrCreate(db.AcademicTerms, x => x.Code == "DEMO-2026-1", new AcademicTerm { Code = "DEMO-2026-1", Name = "Học kỳ 1 năm học 2026-2027", AcademicYear = "2026-2027", SemesterNumber = 1, StartsAt = now.AddMonths(-1), EndsAt = now.AddMonths(5), RegistrationOpensAt = now.AddDays(-7), RegistrationClosesAt = now.AddDays(30), TuitionPerCredit = 650_000m, IsActive = true });

        var teachers = new List<User>(); var students = new List<User>();
        for (var i = 0; i < Count; i++)
        {
            teachers.Add(await User(db, $"giangvien{i + 1:D2}@example.test", TeacherName(i), Roles.Teacher, departments[i % departments.Count].Id, null, [subjects[i].Id]));
            var p = programs[i % programs.Count]; students.Add(await User(db, $"sinhvien{i + 1:D2}@example.test", Students[i], Roles.Student, p.DepartmentId, p.Id, []));
        }
        var admin = await db.Users.Find(x => x.Role == Roles.Admin).FirstOrDefaultAsync();
        for (var i = 0; i < Count; i++) await SeedRow(db, now, i, departments[i % departments.Count], subjects[i], term, teachers[i], students[i], admin?.Id ?? teachers[i].Id);
    }

    private static async Task SeedRow(MongoContext db, DateTime now, int i, Department department, Subject subject, AcademicTerm term, User teacher, User student, ObjectId creator)
    {
        var n = i + 1; var classroom = await GetOrCreate(db.Classes, x => x.Name == $"{subject.Code}-01", new ClassRoom { Name = $"{subject.Code}-01", Description = $"Lớp học phần {subject.Name}.", SubjectId = subject.Id, AcademicTermId = term.Id, EnrollmentCapacity = 45, TeacherId = teacher.Id, StudentIds = [student.Id], CreatedAt = now.AddDays(-n) });
        await db.Classes.UpdateOneAsync(x => x.Id == classroom.Id, Builders<ClassRoom>.Update.AddToSet(x => x.StudentIds, student.Id));
        await db.StudentRegistrations.UpdateOneAsync(x => x.StudentId == student.Id && x.ClassId == classroom.Id, Builders<StudentRegistration>.Update.SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.ClassId, classroom.Id).SetOnInsert(x => x.AcademicTermId, term.Id).SetOnInsert(x => x.Status, "Active").SetOnInsert(x => x.RegisteredAt, now.AddDays(-30)), new UpdateOptions { IsUpsert = true });
        await GetOrCreate(db.Lessons, x => x.ClassId == classroom.Id && x.Title == $"Tổng quan {subject.Name}", new Lesson { ClassId = classroom.Id, Title = $"Tổng quan {subject.Name}", Content = $"Mục tiêu, khái niệm cốt lõi và tình huống vận dụng của {subject.Name}.", IsPublished = true });
        var assignment = await GetOrCreate(db.Assignments, x => x.ClassId == classroom.Id && x.Title == $"Bài tập phân tích {n:D2}", new Assignment { ClassId = classroom.Id, Title = $"Bài tập phân tích {n:D2}", Description = $"Phân tích tình huống thực tế liên quan đến {subject.Name}.", DueDate = now.AddDays(15 + i % 20), IsPublished = true });
        await db.Submissions.UpdateOneAsync(x => x.AssignmentId == assignment.Id && x.StudentId == student.Id, Builders<Submission>.Update.SetOnInsert(x => x.AssignmentId, assignment.Id).SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.Content, $"Bài phân tích của {student.FullName} cho học phần {subject.Name}.").SetOnInsert(x => x.Grade, 6.5 + i % 8 * .5).SetOnInsert(x => x.TeacherComment, "Lập luận rõ ràng; cần bổ sung dẫn chứng.").SetOnInsert(x => x.SubmittedAt, now.AddDays(-5)).SetOnInsert(x => x.GradedAt, now.AddDays(-4)), new UpdateOptions { IsUpsert = true });
        var amount = checked((long)(term.TuitionPerCredit * 3));
        await db.TuitionInvoices.UpdateOneAsync(x => x.StudentId == student.Id && x.ClassId == classroom.Id, Builders<TuitionInvoice>.Update.SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.ClassId, classroom.Id).SetOnInsert(x => x.AcademicTermId, term.Id).SetOnInsert(x => x.Credits, 3).SetOnInsert(x => x.TuitionPerCredit, term.TuitionPerCredit).SetOnInsert(x => x.AmountVnd, amount).SetOnInsert(x => x.Status, TuitionInvoiceStatuses.Pending).SetOnInsert(x => x.CreatedAt, now.AddDays(-30)).SetOnInsert(x => x.UpdatedAt, now.AddDays(-30)), new UpdateOptions { IsUpsert = true });
        var availability = await GetOrCreate(db.TeacherAvailabilities, x => x.TeacherId == teacher.Id && x.Note == $"Khung giờ {n:D2}", new TeacherAvailability { TeacherId = teacher.Id, StartAt = now.AddDays(i % 14 + 1).Date.AddHours(8), EndAt = now.AddDays(i % 14 + 1).Date.AddHours(11), Note = $"Khung giờ {n:D2}", Status = ScheduleStatuses.Pending });
        var session = await GetOrCreate(db.ClassSessions, x => x.ClassId == classroom.Id && x.StartAt == now.Date.AddDays(i % 14 + 1).AddHours(8), new ClassSession { ClassId = classroom.Id, TeacherId = teacher.Id, AvailabilityId = availability.Id, SubjectId = subject.Id, StartAt = now.Date.AddDays(i % 14 + 1).AddHours(8), EndAt = now.Date.AddDays(i % 14 + 1).AddHours(10), Capacity = 45, EnrolledCount = 1, Status = ScheduleStatuses.Scheduled, CreatedBy = creator });
        await db.SessionEnrollments.UpdateOneAsync(x => x.SessionId == session.Id && x.StudentId == student.Id, Builders<SessionEnrollment>.Update.SetOnInsert(x => x.SessionId, session.Id).SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.Status, ScheduleStatuses.Active).SetOnInsert(x => x.EnrolledAt, now.AddDays(-2)), new UpdateOptions { IsUpsert = true });
        await SeedModern(db, now, i, department, subject, classroom, teacher, student);
    }

    private static async Task SeedModern(MongoContext db, DateTime now, int i, Department department, Subject subject, ClassRoom classroom, User teacher, User student)
    {
        var n = i + 1; await GetOrCreate(db.Resources, x => x.OwnerId == teacher.Id, new LearningResource { OwnerId = teacher.Id, PublishedVersion = 1, Versions = [new ResourceVersion { Number = 1, Title = $"Tài liệu {subject.Name}", Description = "Đề cương và tài liệu tham khảo minh họa.", Category = "Tài liệu tham khảo", AuthorName = teacher.FullName, Source = "Tài liệu biên soạn", License = "Sử dụng nội bộ", UsageTerms = "Chỉ dùng cho học tập.", Access = "Subjects", ExternalUrl = "https://example.com/learning-resource", SubjectIds = [subject.Id], EditorId = teacher.Id, EditorName = teacher.FullName, State = ResourceStates.Published }] });
        var question = await GetOrCreate(db.Questions, x => x.SubjectId == subject.Id && x.Content == $"Câu hỏi ôn tập {n:D2} của {subject.Name}?", new QuestionBankItem { OwnerId = teacher.Id, SubjectId = subject.Id, Topic = "Kiến thức nền tảng", Difficulty = i % 3 == 0 ? "Easy" : i % 3 == 1 ? "Medium" : "Hard", Content = $"Câu hỏi ôn tập {n:D2} của {subject.Name}?", Options = [new() { Id = "A", Text = "Phương án phù hợp" }, new() { Id = "B", Text = "Phương án chưa phù hợp" }, new() { Id = "C", Text = "Không đủ dữ kiện" }, new() { Id = "D", Text = "Tất cả đều sai" }], CorrectOption = 0, Explanation = "Phương án A phù hợp với nội dung minh họa.", State = QuestionStates.Published, EditorId = teacher.Id, EditorName = teacher.FullName });
        var quiz = await GetOrCreate(db.Quizzes, x => x.ClassId == classroom.Id, new Quiz { ClassId = classroom.Id, OwnerId = teacher.Id, Title = $"Kiểm tra nhanh {n:D2}", Instructions = "Chọn một đáp án đúng.", DurationMinutes = 15, OpensAt = now.AddDays(-10), ClosesAt = now.AddDays(30), QuestionIds = [question.Id], IsPublished = true });
        var snapshot = new List<QuizQuestionSnapshot> { new() { SourceQuestionId = question.Id, Content = question.Content, Options = question.Options, CorrectOption = 0, Explanation = question.Explanation } };
        await db.QuizAttempts.UpdateOneAsync(x => x.QuizId == quiz.Id && x.StudentId == student.Id, Builders<QuizAttempt>.Update.SetOnInsert(x => x.QuizId, quiz.Id).SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.Questions, snapshot).SetOnInsert(x => x.Answers, new List<int> { i % 5 == 0 ? 1 : 0 }).SetOnInsert(x => x.StartedAt, now.AddDays(-3)).SetOnInsert(x => x.ExpiresAt, now.AddDays(-3).AddMinutes(15)).SetOnInsert(x => x.SubmittedAt, now.AddDays(-3).AddMinutes(8)).SetOnInsert(x => x.Score, i % 5 == 0 ? 0 : 10), new UpdateOptions { IsUpsert = true });
        var ticket = await GetOrCreate(db.SupportTickets, x => x.StudentId == student.Id, new SupportTicket { StudentId = student.Id, DepartmentId = department.Id, Subject = $"Hỗ trợ học tập {n:D2}", Category = "Học tập", Priority = i % 10 == 0 ? "High" : "Normal", Status = i % 4 == 0 ? TicketStatuses.Open : i % 4 == 1 ? TicketStatuses.InProgress : i % 4 == 2 ? TicketStatuses.Resolved : TicketStatuses.Closed, Messages = [new() { SenderId = student.Id, SenderName = student.FullName, SenderRole = Roles.Student, Content = $"Em cần hướng dẫn thêm về học phần {subject.Name}." }] });
        await db.LearningEvents.UpdateOneAsync(x => x.Type == "demo.learning" && x.EntityId == ticket.Id, Builders<LearningEvent>.Update.SetOnInsert(x => x.Type, "demo.learning").SetOnInsert(x => x.ActorId, student.Id).SetOnInsert(x => x.ActorRole, Roles.Student).SetOnInsert(x => x.StudentId, student.Id).SetOnInsert(x => x.DepartmentId, department.Id).SetOnInsert(x => x.ClassId, classroom.Id).SetOnInsert(x => x.EntityType, "ClassRoom").SetOnInsert(x => x.EntityId, ticket.Id).SetOnInsert(x => x.Summary, "Đã hoàn thành hoạt động học tập minh họa").SetOnInsert(x => x.OccurredAt, now.AddDays(-(i % 20))), new UpdateOptions { IsUpsert = true });
    }

    private static string TeacherName(int i) { string[] f = ["Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Vũ", "Đỗ", "Bùi", "Đặng", "Ngô"]; string[] g = ["Minh Quân", "Thu Hương", "Đức Thành", "Ngọc Anh", "Quang Huy"]; return $"{f[i % 10]} {g[i / 10]}"; }
    private static async Task<T> GetOrCreate<T>(IMongoCollection<T> c, System.Linq.Expressions.Expression<Func<T, bool>> f, T value) { var old = await c.Find(f).FirstOrDefaultAsync(); if (old is not null) return old; await c.InsertOneAsync(value); return value; }
    private static async Task<User> User(MongoContext db, string email, string name, string role, ObjectId department, ObjectId? program, IEnumerable<ObjectId> subjects) { var old = await db.Users.Find(x => x.Email == email).FirstOrDefaultAsync(); if (old is not null) return old; var user = new User { FullName = name, Email = email, PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), Role = role, DepartmentId = department, ProgramId = program, CurrentSemester = 1, TeachingSubjectIds = subjects.ToList(), PermissionCodes = PermissionCodes.DefaultsForRole(role).ToList(), PermissionsInitialized = true, StudentPortalPermissionsInitialized = role == Roles.Student, AcademicProfileInitialized = true, IsActive = true }; await db.Users.InsertOneAsync(user); return user; }
}
