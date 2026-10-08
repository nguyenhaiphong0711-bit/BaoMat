namespace LMS.Models;

public static class PermissionCodes
{
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
        (LessonsView, "Xem bài học", "Bài học", "Xem nội dung bài học đã xuất bản trong lớp được tham gia."),
        (LessonsManage, "Quản lý bài học", "Bài học", "Tạo, sửa và lưu trữ bài học trong lớp được phụ trách."),
        (AssignmentsView, "Xem bài tập", "Bài tập", "Xem bài tập đã xuất bản trong lớp được tham gia."),
        (AssignmentsManage, "Quản lý bài tập", "Bài tập", "Tạo, sửa và lưu trữ bài tập trong lớp được phụ trách."),
        (SubmissionsSubmit, "Nộp bài", "Bài tập", "Nộp nội dung và tệp cho bài tập thuộc lớp đã tham gia."),
        (SubmissionsReview, "Xem và chấm bài", "Bài tập", "Xem bài nộp và chấm điểm trong lớp được phụ trách."),
        (ClassesView, "Xem lớp học", "Lớp học", "Xem thông tin các lớp được phân công hoặc đã đăng ký."),
        (ScheduleView, "Xem lịch học", "Lịch học", "Xem lịch của lớp mà tài khoản được tham gia."),
        (ScheduleAvailability, "Đăng ký giờ có thể dạy", "Lịch học", "Đăng ký hoặc cập nhật khung giờ có thể dạy."),
        (ScheduleManage, "Quản lý buổi học", "Lịch học", "Tạo, cập nhật hoặc hủy buổi học trong phạm vi được phân công."),
        (ScheduleEnroll, "Đăng ký buổi học", "Lịch học", "Đăng ký hoặc hủy đăng ký buổi học."),
        (CourseRegistrationView, "Xem chương trình và lớp mở", "Ghi danh", "Xem lớp được mở trong chương trình và học kỳ của mình."),
        (CourseRegistrationManage, "Đăng ký hoặc rút lớp", "Ghi danh", "Ghi danh hoặc rút khỏi lớp trong thời gian đăng ký."),
        (StudentProgressView, "Xem môn học và điểm số", "Học tập", "Xem các môn đã đăng ký, tín chỉ và điểm bài tập của bản thân."),
        (TuitionView, "Xem học phí", "Học phí", "Xem các khoản học phí theo môn, tín chỉ và trạng thái thanh toán của bản thân."),
        (TuitionPay, "Thanh toán học phí", "Học phí", "Khởi tạo thanh toán VNPAY cho khoản học phí của bản thân.")
    ];

    public static IReadOnlyList<string> DefaultsForRole(string role) => role switch
    {
        Roles.Teacher =>
        [
            LessonsView, LessonsManage, AssignmentsView, AssignmentsManage,
            SubmissionsReview, ScheduleView, ScheduleAvailability, ScheduleManage,
            ClassesView
        ],
        Roles.Student =>
        [
            LessonsView, AssignmentsView, SubmissionsSubmit, ScheduleView,
            ScheduleEnroll, CourseRegistrationView, CourseRegistrationManage,
            StudentProgressView, TuitionView, TuitionPay,
            ClassesView
        ],
        _ => Array.Empty<string>()
    };
}
