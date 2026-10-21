namespace LMS.Models;

public static class PermissionCodes
{
    public const string ResourcesView = "resources.view";
    public const string ResourcesManage = "resources.manage";
    public const string ResourcesReview = "resources.review";
    public const string QuestionsView = "questions.view";
    public const string QuestionsManage = "questions.manage";
    public const string QuestionsReview = "questions.review";
    public const string QuizzesView = "quizzes.view";
    public const string QuizzesManage = "quizzes.manage";
    public const string QuizzesAttempt = "quizzes.attempt";
    public const string StaffStudentsView = "staff.students.view";
    public const string StaffProgressView = "staff.progress.view";
    public const string ClaimType = "lms:permission";
    public const string PolicyPrefix = "Permission:";

    public const string LessonsView = "lessons.view";
    public const string LessonsManage = "lessons.manage";
    public const string AssignmentsView = "assignments.view";
    public const string AssignmentsManage = "assignments.manage";
    public const string SubmissionsSubmit = "submissions.submit";
    public const string SubmissionsReview = "submissions.review";
    public const string ClassesView = "classes.view";
    public const string ScheduleView = "schedule.view";
    public const string ScheduleAvailability = "schedule.availability";
    public const string ScheduleManage = "schedule.manage";
    public const string ScheduleEnroll = "schedule.enroll";
    public const string CourseRegistrationView = "course-registration.view";
    public const string CourseRegistrationManage = "course-registration.manage";
    public const string StudentProgressView = "student-progress.view";
    public const string TuitionView = "tuition.view";
    public const string TuitionPay = "tuition.pay";

    public static IReadOnlyList<(string Code, string Name, string Category, string Description)> SystemPermissions { get; } =
    [
        (ResourcesView, "Xem kho học liệu", "Kho học liệu", "Xem tài liệu đã công bố theo quyền truy cập và môn/lớp được tham gia."),
        (ResourcesManage, "Soạn và công bố học liệu", "Kho học liệu", "Quản lý tài liệu của mình trong các môn được phân công; chỉ công bố phiên bản đã được duyệt."),
        (ResourcesReview, "Duyệt học liệu trong khoa", "Kho học liệu", "Duyệt tài liệu đã gửi thuộc khoa được giao; không tự duyệt tài liệu do mình soạn."),
        (QuestionsView, "Xem ngân hàng câu hỏi", "Ngân hàng câu hỏi", "Xem câu hỏi thuộc môn được phân công hoặc khoa được giao."),
        (QuestionsManage, "Soạn câu hỏi", "Ngân hàng câu hỏi", "Soạn và công bố câu hỏi trắc nghiệm trong môn được phân công."),
        (QuestionsReview, "Duyệt câu hỏi trong khoa", "Ngân hàng câu hỏi", "Duyệt câu hỏi thuộc khoa được giao; không tự duyệt câu hỏi do mình soạn."),
        (QuizzesView, "Xem bài kiểm tra", "Bài kiểm tra", "Xem bài kiểm tra trong lớp được phân công hoặc tham gia."),
        (QuizzesManage, "Quản lý bài kiểm tra", "Bài kiểm tra", "Tạo bài kiểm tra từ câu hỏi đã công bố và xem kết quả của lớp được phụ trách."),
        (QuizzesAttempt, "Làm bài kiểm tra", "Bài kiểm tra", "Bắt đầu và nộp một lượt làm bài trong lớp đang tham gia."),
        (StaffStudentsView, "Xem sinh viên trong khoa", "Cán bộ quản lý", "Xem hồ sơ cơ bản của sinh viên thuộc khoa được Admin phân công."),
        (StaffProgressView, "Theo dõi học tập trong khoa", "Cán bộ quản lý", "Xem số bài nộp và điểm trung bình bài tập của sinh viên trong các lớp thuộc khoa được giao."),
        (LessonsView, "Xem bài học", "Bài học", "Sinh viên xem bài đã công bố; giáo viên xem cả bản nháp trong lớp được phân công."),
        (LessonsManage, "Quản lý bài học", "Bài học", "Tạo, sửa và lưu trữ bài học trong lớp được phụ trách."),
        (AssignmentsView, "Xem bài tập", "Bài tập", "Sinh viên xem bài đã công bố; giáo viên xem cả bản nháp trong lớp được phân công."),
        (AssignmentsManage, "Quản lý bài tập", "Bài tập", "Tạo, sửa và lưu trữ bài tập trong lớp được phụ trách."),
        (SubmissionsSubmit, "Nộp bài", "Bài tập", "Nộp nội dung và tệp cho bài tập thuộc lớp đã tham gia."),
        (SubmissionsReview, "Xem và chấm bài", "Bài tập", "Xem bài nộp và chấm điểm trong lớp được phụ trách."),
        (ClassesView, "Xem lớp học", "Lớp học", "Xem thông tin các lớp được phân công hoặc đã đăng ký."),
        (ScheduleView, "Xem lịch học", "Lịch học", "Xem lịch của lớp mà tài khoản được tham gia."),
        (ScheduleAvailability, "Đăng ký giờ có thể dạy", "Lịch học", "Đăng ký hoặc cập nhật khung giờ có thể dạy."),
        (ScheduleManage, "Hủy buổi học được phân công", "Lịch học", "Giáo viên hủy buổi học của lớp mình phụ trách. Tạo và xếp lịch do Admin thực hiện."),
        (ScheduleEnroll, "Đăng ký buổi học", "Lịch học", "Đăng ký hoặc hủy đăng ký buổi học."),
        (CourseRegistrationView, "Xem chương trình và lớp mở", "Ghi danh", "Xem lớp được mở trong chương trình và học kỳ của mình."),
        (CourseRegistrationManage, "Đăng ký hoặc rút lớp", "Ghi danh", "Ghi danh hoặc rút khỏi lớp trong thời gian đăng ký."),
        (StudentProgressView, "Xem môn học và điểm số", "Học tập", "Xem các môn đã đăng ký, tín chỉ và điểm bài tập của bản thân."),
        (TuitionView, "Xem học phí", "Học phí", "Xem các khoản học phí theo môn, tín chỉ và trạng thái thanh toán của bản thân."),
        (TuitionPay, "Thanh toán học phí", "Học phí", "Khởi tạo thanh toán VNPAY cho khoản học phí của bản thân.")
    ];

    public static IReadOnlyList<string> RequiredFor(string code) => code switch
    {
        ResourcesManage or ResourcesReview => [ResourcesView],
        QuestionsManage or QuestionsReview => [QuestionsView],
        QuizzesManage or QuizzesAttempt => [QuizzesView],
        StaffProgressView => [StaffStudentsView],
        LessonsView or AssignmentsView => [ClassesView],
        LessonsManage => [LessonsView],
        AssignmentsManage or SubmissionsSubmit or SubmissionsReview => [AssignmentsView],
        ScheduleAvailability or ScheduleManage or ScheduleEnroll => [ScheduleView],
        CourseRegistrationManage => [CourseRegistrationView],
        TuitionPay => [TuitionView],
        _ => Array.Empty<string>()
    };

    // Only remove invalid grants; never silently add permissions that an Admin revoked.
    public static List<string> EffectiveFor(string role, IEnumerable<string> grants, IEnumerable<Permission> definitions)
    {
        if (role is not (Roles.Admin or Roles.Teacher or Roles.Student or Roles.Staff)) return new();
        var allowed = definitions.Where(p => IsAllowedForRole(role, p.Code) &&
            (role == Roles.Admin || p.AllowedRoles.Contains(role))).ToDictionary(p => p.Code);
        var result = grants.Where(allowed.ContainsKey).ToHashSet(StringComparer.Ordinal);
        while (result.RemoveWhere(code => allowed[code].RequiredCodes.Any(required => !result.Contains(required))) > 0) { }
        return result.Order(StringComparer.Ordinal).ToList();
    }

    // Custom grants add restrictions; they never replace role or resource ownership checks.
    public static bool IsAllowedForRole(string role, string code) =>
        role == Roles.Admin ||
        (role is Roles.Teacher or Roles.Student or Roles.Staff &&
         (!SystemPermissions.Any(p => p.Code == code) || DefaultsForRole(role).Contains(code, StringComparer.Ordinal)));

    public static IReadOnlyList<string> DefaultsForRole(string role) => role switch
    {
        Roles.Staff => [StaffStudentsView, StaffProgressView, ResourcesView, ResourcesReview, QuestionsView, QuestionsReview],
        Roles.Teacher =>
        [
            ResourcesView, ResourcesManage, QuestionsView, QuestionsManage, QuizzesView, QuizzesManage, LessonsView, LessonsManage, AssignmentsView, AssignmentsManage,
            SubmissionsReview, ScheduleView, ScheduleAvailability, ScheduleManage,
            ClassesView
        ],
        Roles.Student =>
        [
            ResourcesView, QuizzesView, QuizzesAttempt, LessonsView, AssignmentsView, SubmissionsSubmit, ScheduleView,
            ScheduleEnroll, CourseRegistrationView, CourseRegistrationManage,
            StudentProgressView, TuitionView, TuitionPay,
            ClassesView
        ],
        _ => Array.Empty<string>()
    };
}
