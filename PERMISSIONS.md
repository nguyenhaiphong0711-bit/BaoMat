# Phân quyền và đồng bộ MongoDB

Ứng dụng hiện có bốn vai trò: Admin, Teacher, Student, Staff. Vai trò giới hạn loại thao tác; quyền chức năng giới hạn chức năng được dùng; backend tiếp tục kiểm tra lớp, chủ bài nộp và trạng thái công bố. Cấp một quyền không cho phép đọc lớp hoặc bài nộp của người khác.

| Vai trò | Phạm vi |
|---|---|
| Admin | Quản lý tài khoản, quyền, khoa/ngành, môn/lớp, phân công, duyệt đăng ký và xếp lịch. Có toàn quyền qua vai trò; không cần gán từng quyền. |
| Teacher | Xem lớp được phân công; quản lý bài học/bài tập, chấm bài trong lớp đó; đăng ký giờ có thể dạy và hủy buổi học mình phụ trách. Không tạo tài khoản, tự mở lớp hoặc xếp lịch. |
| Staff | Xem hồ sơ và tổng hợp học tập của sinh viên trong khoa được Admin giao. Không có khoa thì không được xem dữ liệu. Xem [STAFF.md](STAFF.md). |
| Student | Xem lớp đã tham gia và nội dung đã công bố; nộp/xem bài của mình; xem tiến trình, học phí, thanh toán và đăng ký học của bản thân. Không chấm điểm hoặc xem bài nộp của bạn khác. |

## Quyền chức năng hiện có

| Mã quyền | Vai trò được gán | Quyền cần có trước |
|---|---|---|
| resources.view | Teacher, Student, Staff | — |
| resources.manage | Teacher | resources.view |
| resources.review | Staff | resources.view |
| questions.view | Teacher, Staff | — |
| questions.manage | Teacher | questions.view |
| questions.review | Staff | questions.view |
| quizzes.view | Teacher, Student | — |
| quizzes.manage | Teacher | quizzes.view |
| quizzes.attempt | Student | quizzes.view |
| staff.students.view | Staff | — |
| staff.progress.view | Staff | staff.students.view |
| classes.view | Teacher, Student | — |
| lessons.view | Teacher, Student | classes.view |
| lessons.manage | Teacher | lessons.view |
| assignments.view | Teacher, Student | classes.view |
| assignments.manage | Teacher | assignments.view |
| submissions.submit | Student | assignments.view |
| submissions.review | Teacher | assignments.view |
| schedule.view | Teacher, Student | — |
| schedule.availability | Teacher | schedule.view |
| schedule.manage | Teacher | schedule.view |
| schedule.enroll | Student | schedule.view |
| course-registration.view | Student | — |
| course-registration.manage | Student | course-registration.view |
| student-progress.view | Student | — |
| tuition.view | Student | — |
| tuition.pay | Student | tuition.view |

`schedule.manage` hiện cho giáo viên hủy buổi học thuộc lớp được phân công. Tạo/xếp lịch vẫn dành cho Admin; tên và mô tả quyền đã được sửa để phản ánh điều này.

Ba quyền kho học liệu mới cần được Admin cấp cho tài khoản đã có; không tự cấp lại/bổ sung vào bộ quyền đã tùy chỉnh. Xem [RESOURCES.md](RESOURCES.md).

Màn hình Admin → Phân quyền chỉ hiển thị quyền phù hợp vai trò. Chọn quyền thao tác tự chọn đủ quyền xem phụ thuộc; bỏ quyền xem sẽ bỏ các quyền phụ thuộc. Backend từ chối biểu mẫu giả mạo thiếu quyền cần thiết, không tự thêm quyền khi lưu.

Quyền tùy chỉnh chỉ bổ sung điều kiện hạn chế cho action được ánh xạ, không vượt qua Authorize/RequirePermission hay phạm vi tài nguyên. Thêm một mã vào database không tự tạo chức năng mới. Các bản ghi tùy chỉnh cũ mặc định cho Teacher/Student; trường AllowedRoles có thể hạn chế thêm, và RequiredCodes xác định quyền phụ thuộc.

## Dữ liệu được cập nhật

Khi khởi động bình thường, `PermissionService.InitializeAsync`:

1. Upsert 27 quyền hệ thống vào collection `Permissions`, cập nhật tên/mô tả, `AllowedRoles`, `RequiredCodes`. Giữ nguyên các định nghĩa tùy chỉnh.
2. Tài khoản cũ chưa từng khởi tạo được cấp bộ quyền mặc định theo vai trò. Quyền cổng sinh viên cũ chỉ được bổ sung một lần theo cờ migration đã có.
3. Với tài khoản đã khởi tạo, loại mã không có định nghĩa, sai vai trò, trùng hoặc thiếu quyền phụ thuộc. Giữ quyền tùy chỉnh còn hợp lệ. Không khôi phục quyền Admin đã thu hồi.
4. Lưu Before/After theo UserId trong `PermissionMigrations` trước khi cập nhật. `Applied=true` cho biết snapshot đã được áp dụng. Snapshot không chứa mật khẩu.
5. So khớp vai trò, danh sách quyền và cờ migration trước khi cập nhật để không ghi đè thay đổi đồng thời. Nếu có xung đột, snapshot có Applied=false; dữ liệu mới được giữ và quyền hiệu lực vẫn được lọc khi truy cập. Lần đồng bộ tiếp theo sẽ thử lại.

Chạy lại không thêm quyền hoặc snapshot khi dữ liệu đã chuẩn. Không xóa tài khoản, môn học, lớp, bài nộp hay tệp. Không cần đổi MongoDB sang SQL Server.

Nếu muốn chỉ đồng bộ quyền, không mở web server hoặc seed học vụ:

```bash
dotnet run -- --sync-permissions
```

Lệnh dùng cấu hình database hiện có của ứng dụng. Trên máy cá nhân, sau khi pull code, chỉ cần dừng phiên đang chạy rồi `dotnet run`; quá trình khởi động tự cập nhật database của máy đó. Database cloud và database trên máy cá nhân là hai môi trường riêng nếu cấu hình kết nối khác nhau.

Khi cần đối chiếu, Admin có thể xem snapshot trong `PermissionMigrations` bằng MongoDB Compass. Không khôi phục toàn bộ snapshot một cách tự động vì có thể cấp lại quyền đã thu hồi sau đó.

## Thu hồi quyền đang sử dụng

Cookie được đối chiếu database ở mỗi request. Quyền hiệu lực được lọc theo định nghĩa đang tồn tại, AllowedRoles, vai trò và đủ quyền phụ thuộc, cả lúc đăng nhập và khi kiểm tra tệp tải xuống. Thay đổi quyền áp dụng ở request tiếp theo; thay đổi vai trò/khóa tài khoản làm phiên cũ mất hiệu lực. React cập nhật menu khi tải lại phiên/trang; backend không chờ giao diện cập nhật mới chặn quyền.

## Kiểm thử

```bash
python tests/permission_integration.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
```

Runner dùng database Mongo tạm, kiểm thử bảo mật hiện có, sửa trực tiếp quyền trong database thử nghiệm, tài khoản cũ thiếu trường, giữ quyền thu hồi/tùy chỉnh, snapshot và tính lặp lại của migration; kiểm thử trình duyệt cho quy tắc chọn quyền và luồng học tập React.

Vai trò Staff đã được bổ sung với phạm vi một khoa và các báo cáo chỉ đọc. Xem [STAFF.md](STAFF.md) để cấu hình; phạm vi nhiều khoa/chương trình và quy trình xử lý hỗ trợ chưa triển khai.

Kết quả kiểm tra bản chuẩn hóa trước khi thêm Staff: build .NET 0 lỗi/0 cảnh báo; 12/12 ca Chromium và 33 nhóm hồi quy đạt. Database cloud `LMS_Cloud_Dev` đã đồng bộ 16 định nghĩa, kiểm tra 3 tài khoản không còn quyền sai vai trò/phụ thuộc; 2 snapshot cập nhật được lưu. Đây là dữ liệu cloud, không xác nhận database trên máy cá nhân đã cập nhật trước khi chạy bản mới.
