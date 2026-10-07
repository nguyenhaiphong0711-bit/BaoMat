# LMS Secure - ASP.NET Core MVC + REST API + MongoDB

Bài tập nhỏ môn học, tập trung vào MVC, REST API và security. UI admin-style dùng chung nghiệp vụ với API.

## Công nghệ
- ASP.NET Core MVC / Web API .NET 10
- MongoDB
- MongoDB.Driver
- Cookie Authentication
- BCrypt password hashing
- Razor Views

Danh mục học vụ được lưu trong MongoDB database `LMS_Secure`: `Departments` chứa khoa, `Subjects` chứa môn và `Classes` chứa lớp. Các entity MongoDB được tách thành file riêng trong `Models/`; `SeedData` tạo khoa mặc định và gắn môn cũ chưa có khoa vào đó mà không xóa dữ liệu.

## Giao diện
- Bố cục điều hướng bên trái theo vai trò, thanh tiêu đề và vùng nội dung có chiều rộng nhất quán.
- Dashboard có lối tắt theo quyền; trang danh mục dùng bảng có thể cuộn trên màn hình nhỏ; biểu mẫu, trạng thái, thông báo và thẻ nội dung dùng chung hệ thống giao diện.
- CSS được gắn version tự động để trình duyệt tải giao diện mới sau khi build.

## Chạy project
1. Cài .NET 10 SDK.
2. Cài MongoDB local và chạy MongoDB service.
3. Kiểm tra `appsettings.json`.
4. Chạy:

```bash
dotnet restore
dotnet run
```

Mở `http://localhost:5000`.

## Deploy lên Render bằng Docker
1. Đẩy project lên GitHub và tạo một **Web Service** trên Render, chọn repository này và runtime **Docker**. Render sẽ dùng `Dockerfile` ở thư mục gốc.
2. Tạo MongoDB database có thể truy cập từ Render (ví dụ MongoDB Atlas), sau đó thêm các Environment Variables cho Web Service:

   | Key | Value |
   |---|---|
   | `MongoDb__ConnectionString` | MongoDB connection URI |
   | `MongoDb__DatabaseName` | `LMS_Secure` hoặc tên database mong muốn |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   Đặt URI trong Render, không commit credentials vào repository. Không dùng `localhost` làm MongoDB host trên Render: trong container, `localhost` trỏ tới chính container ứng dụng.
3. Chọn **Create Web Service**. Container bind tới `0.0.0.0` và dùng biến `PORT` Render cung cấp (mặc định dự phòng `10000`).
4. Đảm bảo MongoDB cho phép kết nối mạng từ dịch vụ Render, sau đó mở URL được Render cấp.

**Lưu ý bảo mật:** ứng dụng hiện tự tạo tài khoản demo khi collection `Users` còn trống (`admin@lms.com` / `Admin@123`, cùng tài khoản Teacher/Student demo). Không để các thông tin này làm tài khoản production; trước khi public, hãy chuẩn bị tài khoản quản trị riêng và thay đổi hoặc loại bỏ credentials demo.

## Tài khoản demo
- Admin: `admin@lms.com` / `Admin@123`
- Teacher: `teacher@lms.com` / `Teacher@123`
- Student: `student@lms.com` / `Student@123`

Các tài khoản demo chỉ dành cho môi trường local/dev; hãy thay đổi hoặc xóa chúng trước khi triển khai.

## Kiểm thử API bằng Postman
1. Chạy ứng dụng ở `http://localhost:5000` và đảm bảo MongoDB đang hoạt động.
2. Trong Postman, chọn **Import** và mở `postman/LMS.postman_collection.json`.
3. Chạy lần lượt các request theo thứ tự trong collection. Postman lưu cookie đăng nhập để những request tiếp theo sử dụng cùng phiên.
4. Collection đăng nhập lần lượt bằng Admin, Student và Teacher; tạo môn học, gán môn cho giảng viên, tạo lớp theo môn, quản lý bài học/bài tập, nộp bài, chấm điểm, rồi chọn lớp/môn/ngày giờ trực tiếp để tạo buổi và cho học viên đăng ký. Các request tạo dữ liệu mới khi chạy.

| Method | Endpoint | Quyền |
|---|---|---|
| `POST` | `/api/auth/login` | Anonymous |
| `GET` | `/api/auth/me` | Đã đăng nhập |
| `POST` | `/api/auth/logout` | Đã đăng nhập |
| `GET`, `POST` | `/api/users` | Admin |
| `POST` | `/api/users/{id}/toggle-active` | Admin |
| `GET`, `POST` | `/api/subjects` | Đã đăng nhập đọc / Admin tạo |
| `GET`, `PUT`, `DELETE` | `/api/subjects/{id}` | Đã đăng nhập đọc / Admin sửa, xóa |
| `GET` | `/api/departments` | Đã đăng nhập xem danh sách khoa |
| `GET` | `/api/departments/{id}` | Đã đăng nhập xem khoa |
| `POST` | `/api/departments` | Admin tạo khoa |
| `PUT` | `/api/departments/{id}` | Admin cập nhật khoa |
| `DELETE` | `/api/departments/{id}` | Admin xóa khoa (bị chặn khi còn môn học) |
| `GET`, `PUT` | `/api/teachers/{id}/subjects` | Admin xem và gán môn giảng dạy |
| `GET`, `POST` | `/api/classes` | Đã đăng nhập / Admin tạo |
| `PUT` | `/api/classes/{id}` | Admin cập nhật lớp, môn, giảng viên, danh sách học viên |
| `DELETE` | `/api/classes/{id}` | Admin lưu trữ lớp; cần hủy buổi tương lai trước |
| `PUT` | `/api/classes/{id}/teaching-assignment` | Admin phân công/đổi giảng viên |
| `GET` | `/api/classes/{id}` | Thành viên lớp hoặc Admin |
| `GET`, `POST` | `/api/classes/{classId}/lessons` | Thành viên lớp đọc; Admin/Teacher tạo |
| `GET`, `PUT` | `/api/lessons/{id}` | Thành viên lớp; Student chỉ đọc bài đã publish; Admin/Teacher sửa |
| `GET`, `POST` | `/api/classes/{classId}/assignments` | Thành viên lớp đọc; Admin/Teacher tạo |
| `GET`, `PUT` | `/api/assignments/{id}` | Thành viên lớp; Student chỉ đọc bài đã publish; Admin/Teacher sửa |
| `DELETE` | `/api/assignments/{id}`, `/api/lessons/{id}` | Admin/Teacher lưu trữ bài; nội dung không còn hiện với học viên |
| `GET` | `/api/assignments/{assignmentId}/submissions` | Admin/Teacher thuộc lớp |
| `GET`, `PUT` | `/api/assignments/{assignmentId}/submission` | Student thuộc lớp |
| `PUT` | `/api/submissions/{id}/grade` | Admin/Teacher thuộc lớp |
| `GET` | `/api/activity-logs` | Admin |
| `GET`, `POST` | `/api/schedule/availabilities` | Teacher xem/gửi giờ; Admin xem tất cả |
| `PUT` | `/api/schedule/availabilities/{id}/review` | Admin duyệt/từ chối giờ |
| `GET` | `/api/schedule/sessions` | Thành viên xem lịch |
| `POST` | `/api/schedule/sessions` | Admin tạo buổi bằng lớp, môn, giờ bắt đầu/kết thúc và sức chứa |
| `GET`, `DELETE` | `/api/schedule/sessions/{id}` | Thành viên xem; Admin hoặc Teacher được phân công hủy |
| `POST`, `DELETE` | `/api/schedule/sessions/{id}/enrollment` | Student trong roster đăng ký/hủy đăng ký |

Danh mục học vụ được liên kết theo **Khoa → Môn học → Lớp học**. Khi tạo/sửa môn học cần gửi `departmentId` hợp lệ; lớp chỉ chọn môn thuộc khoa đã chọn. Các danh sách quản trị hỗ trợ tìm kiếm theo từ khóa và lọc theo ngày tạo; lịch học lọc theo ngày diễn ra, lớp, môn và trạng thái. Collection Postman có luồng CRUD khoa/môn và kiểm tra ràng buộc xóa.

API dùng cookie authentication giống MVC, không phát hành bearer/JWT token. Lỗi xác thực/phân quyền trên `/api/*` trả `401`/`403` dạng Problem Details thay vì redirect tới trang HTML. Các response user được lọc, không trả `PasswordHash`, lockout counter hay trường nội bộ khác. Create trả `201 Created`; cập nhật/nộp bài trả `200 OK`; logout trả `204 No Content`.

## Đánh giá authentication/authorization
- Đã có: BCrypt password hashing; lockout theo tài khoản sau số lần sai cấu hình; ghi log login; cookie `HttpOnly`, `SameSite=Lax`, hết hạn 8 giờ và sliding expiration; role authorization; kiểm tra membership/teacher ownership ở server; antiforgery cho form MVC; cookie production chỉ gửi qua HTTPS.
- Đã bổ sung: API login/me/logout; API trả status JSON `401`/`403`; mỗi request xác thực lại trạng thái active và role hiện tại để vô hiệu cookie cũ sau khi khóa tài khoản/đổi role; logout MVC chuyển sang POST có antiforgery (không còn logout bằng GET).
- Chưa đầy đủ cho production: mật khẩu mới bắt buộc tối thiểu 8 ký tự nhưng chưa có MFA, xác minh email, luồng quên/đổi mật khẩu, hoặc giới hạn tốc độ theo IP; lockout hiện chủ yếu theo tài khoản và có thể bị race khi login đồng thời. Thông báo lỗi đăng nhập đã dùng nội dung chung để hạn chế dò tài khoản. Hiện phù hợp demo/bài tập hơn là yêu cầu production có rủi ro cao.

## Requirement đã triển khai

- Admin tạo tài khoản Admin/Teacher/Student.
- Admin tạo class, gán teacher và students.
- Admin CRUD danh mục môn học; không cho xóa môn còn được lớp hoặc giáo viên sử dụng.
- Admin CRUD khoa; không cho xóa khoa đang có môn học tham chiếu; môn học thuộc một khoa và lớp sử dụng môn đó.
- Admin gán nhiều môn cho từng giáo viên; không cho bỏ môn đang được dùng bởi lớp của giáo viên.
- Admin tạo class theo môn, chỉ gán được giáo viên có môn tương ứng; bài học, bài tập và lịch kế thừa môn của lớp.
- Admin thay đổi danh sách học viên, môn và phân công lại giảng viên cho lớp; không đổi môn/giảng viên khi lớp có buổi sắp tới.
- Teacher chỉ thao tác class mình phụ trách.
- Student chỉ truy cập class mình tham gia.
- Teacher tạo/publish lesson và assignment.
- Teacher chỉnh sửa lesson và assignment trong lớp được phân công.
- Teacher có thể khai báo giờ rảnh để tham khảo; Admin tạo buổi trực tiếp bằng lớp, môn, ngày giờ và sức chứa. Giảng viên được xác định theo lớp và phải được phân công dạy môn tương ứng.
- Student đăng ký buổi học của lớp mình; hệ thống kiểm tra trùng lịch, số chỗ và chỉ cho hủy trước khi buổi bắt đầu.
- Hủy buổi học giữ lịch sử, chuyển enrollment sang cancelled; hủy đăng ký không xóa bản ghi lịch sử.
- Lớp/bài học/bài tập có thao tác lưu trữ mềm để không làm mất bài nộp và lịch sử; lớp có buổi sắp tới phải hủy lịch trước khi lưu trữ.
- Lịch được lưu trong `TeacherAvailabilities`, `ClassSessions`, `SessionEnrollments`; giờ được lưu theo UTC, màn hình hiển thị theo giờ địa phương. Mỗi buổi có sức chứa riêng và hệ thống kiểm tra trùng giờ trước khi ghi danh.
- Student chỉ thấy lesson/assignment đã publish và được submit.
- Teacher/Admin xem submission thuộc class được phép và grade.
- Password hash bằng BCrypt.
- 5 lần login sai -> lock 15 phút.
- Mỗi login attempt ghi ActivityLog.
- Cookie authentication + role authorization.
- Server-side class access check để chống truy cập ID của class khác.
- REST API cho authentication, user, class, lesson, assignment, submission, grading và activity log.
- Dashboard và menu điều hướng theo vai trò; học viên xem điểm/nhận xét đã chấm ngay trong màn hình nộp bài.

## Cấu trúc
Controllers / Models / Services / Data / Views / wwwroot.

`Services` được dùng để giữ business logic để Controller không quá dài.
"# BaoMat" 
