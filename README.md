# LMS Secure - ASP.NET Core MVC + REST API + MongoDB

Bài tập nhỏ môn học, tập trung vào MVC, REST API và security. UI admin-style dùng chung nghiệp vụ với API.

## Công nghệ
- ASP.NET Core MVC / Web API .NET 10
- MongoDB
- MongoDB.Driver
- Cookie Authentication
- BCrypt password hashing
- Razor Views

Danh mục học vụ được lưu trong MongoDB database `LMS_Secure`: `Departments` chứa khoa, `Subjects` chứa môn, `AcademicPrograms` chứa ngành học, `ProgramCourses` liên kết môn với ngành và học kỳ chương trình, `AcademicTerms` chứa học kỳ, và `Classes` chứa lớp. Trong giao diện quản trị, mục **Ngành học** cho phép tạo/sửa ngành, thêm môn của khoa tương ứng vào đề cương, chọn học kỳ/tín chỉ và lưu trữ/mở lại ngành. Ngành đang được gán cho sinh viên không thể lưu trữ cho đến khi chuyển các hồ sơ đó sang ngành khác. Các entity MongoDB được tách thành file riêng trong `Models/`; `SeedData` tạo dữ liệu mặc định và gắn các bản ghi cũ còn thiếu liên kết mà không xóa dữ liệu.

Quản trị học vụ bổ sung `AcademicPrograms`, `ProgramCourses`, `AcademicTerms` và `StudentRegistrations`. Mỗi hồ sơ sinh viên gắn một chương trình và học kỳ hiện tại; lớp được mở trong một học kỳ và có sức chứa. Khi sinh viên đăng ký, máy chủ kiểm tra đề cương, kỳ đăng ký, sĩ số và trùng lịch rồi ghi danh ngay. Admin có thể quản lý chương trình/đề cương, học kỳ, hồ sơ học vụ và quyền riêng từng tài khoản tại mục **Phân quyền**.

## Giao diện
- Bố cục điều hướng bên trái theo vai trò, thanh tiêu đề và vùng nội dung có chiều rộng nhất quán.
- Dashboard có lối tắt theo quyền; trang danh mục dùng bảng có thể cuộn trên màn hình nhỏ; biểu mẫu, trạng thái, thông báo và thẻ nội dung dùng chung hệ thống giao diện.
- CSS được gắn version tự động để trình duyệt tải giao diện mới sau khi build.
- Các trang danh sách hỗ trợ phân trang 10, 20 hoặc 50 dòng/trang và giữ bộ lọc hiện tại khi chuyển trang.

## Dữ liệu demo và phân trang
Dữ liệu minh họa chỉ được thêm khi chạy môi trường `Development` với `DemoData__Enabled=true`; ví dụ PowerShell: `$env:DemoData__Enabled='true'; dotnet run`. Seeder thêm tối đa 20 bản ghi mẫu cho các danh mục/nghiệp vụ chính (khoa, ngành, môn, lớp, bài học, bài tập, bài nộp, lịch, ghi danh và hóa đơn Pending), có tiền tố `DEMO`, dùng email `@example.test` và không xóa dữ liệu cũ. Các tài khoản mẫu bổ sung dùng mật khẩu `Demo@123`; chỉ bật cờ này với MongoDB local dành riêng cho phát triển, tuyệt đối không bật trong Production. Đơn giá và điểm trong dữ liệu demo không phải biểu phí hay kết quả chính thức của NEU. Các token khôi phục mật khẩu và trạng thái thanh toán thành công không được tạo giả.

## Chạy project
1. Cài .NET 10 SDK.
2. Cài MongoDB local và chạy MongoDB service.
3. Kiểm tra `appsettings.json`.
4. Để gửi mã khôi phục mật khẩu, sao chép `.env.example` thành `.env`, sau đó cấu hình Gmail SMTP bằng **Google App Password** (không dùng mật khẩu đăng nhập Google). Bật xác minh 2 bước cho tài khoản Google, tạo App Password và điền các giá trị trong `.env`:

   | Key | Value |
   |---|---|
   | `Email__Host` | `smtp.gmail.com` (mặc định) |
   | `Email__Port` | `587` |
   | `Email__Username` | Địa chỉ Gmail dùng gửi thư |
   | `Email__AppPassword` | Google App Password 16 ký tự |
   | `Email__SenderEmail` | (Không bắt buộc) mặc định dùng `Email__Username` |
   | `Email__SenderName` | (Không bắt buộc) mặc định `LearnSpace` |

   Ứng dụng tự đọc `.env` khi khởi động; biến môi trường đã được hệ điều hành/Render thiết lập sẽ được ưu tiên. Không đưa App Password vào `appsettings.json`, source control, ảnh chụp hoặc log. `.env` được loại trừ khỏi Git và Docker build; trên Render hãy khai báo cùng các biến này trong **Environment** của Web Service.
5. Chạy:

```bash
dotnet restore
dotnet run
```

Mở `http://localhost:5000`.

## Theo dõi học tập và thanh toán học phí
Sinh viên gửi yêu cầu đăng ký lớp; Admin duyệt tại mục **Duyệt đăng ký**. Chỉ yêu cầu được duyệt mới ghi danh sinh viên vào lớp và tạo hóa đơn. Sinh viên xem môn đang học, tín chỉ đã ghi danh và điểm trung bình các bài tập đã chấm tại **Môn học & điểm**. Điểm này chỉ là trung bình bài tập, chưa phải điểm tổng kết môn có trọng số. Mục **Học phí** hiển thị từng hóa đơn theo lớp/môn. Khi Admin cấu hình đơn giá mỗi tín chỉ trong mục **Học kỳ**, hệ thống tính hóa đơn lúc yêu cầu được duyệt; hóa đơn giữ snapshot đơn giá đó kể cả khi Admin cập nhật biểu phí sau này. Việc rút khỏi lớp đã thanh toán cần Admin xử lý do chưa tích hợp hoàn tiền.

Để bật VNPAY, khai báo các biến sau trong `.env` local hoặc Render **Environment**. Dùng thông tin merchant sandbox khi phát triển và thay bằng thông tin production khi triển khai:

| Key | Value |
|---|---|
| `Vnpay__TmnCode` | Mã website/merchant VNPAY |
| `Vnpay__HashSecret` | Chuỗi bí mật dùng ký HMAC-SHA512; không commit hoặc ghi vào log |
| `Vnpay__PaymentUrl` | `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html` (sandbox) |
| `Vnpay__BankCode` | `MB` để chọn MB Bank làm ngân hàng thanh toán; để trống nếu muốn khách tự chọn |
| `Vnpay__ReturnUrl` | URL public HTTPS của `GET /Vnpay/Return` |
| `Vnpay__IpnUrl` | URL public HTTPS của `GET /Vnpay/Ipn` |

`Vnpay__BankCode=MB` chỉ chọn ngân hàng/ngả thanh toán MB trên VNPAY; nó không đăng ký số tài khoản nhận tiền. Tài khoản nhận thanh toán phải được liên kết với merchant bởi VNPAY. Production yêu cầu Payment URL, Return URL và IPN URL là HTTPS tuyệt đối; IPN phải truy cập được từ VNPAY. Nếu chưa cấu hình merchant, học phí vẫn xem được nhưng không thể tạo yêu cầu thanh toán. Callback chỉ đánh dấu hóa đơn đã trả khi chữ ký, merchant, mã tham chiếu, số tiền, mã phản hồi và trạng thái giao dịch đều hợp lệ. Cần kiểm thử với tài khoản sandbox và URL public được VNPAY cho phép trước khi bật thanh toán thật.

## Deploy lên Render bằng Docker
1. Đẩy project lên GitHub và tạo một **Web Service** trên Render, chọn repository này và runtime **Docker**. Render sẽ dùng `Dockerfile` ở thư mục gốc.
2. Tạo MongoDB database có thể truy cập từ Render (ví dụ MongoDB Atlas), sau đó thêm các Environment Variables cho Web Service:

   | Key | Value |
   |---|---|
   | `MongoDb__ConnectionString` | MongoDB connection URI |
   | `MongoDb__DatabaseName` | `LMS_Secure` hoặc tên database mong muốn |
   | `Email__Host` | `smtp.gmail.com` |
   | `Email__Port` | `587` |
   | `Email__Username` | Địa chỉ Gmail dùng gửi thư |
   | `Email__AppPassword` | Google App Password 16 ký tự |
   | `Email__SenderEmail` | (Không bắt buộc) mặc định dùng `Email__Username` |
   | `Email__SenderName` | (Không bắt buộc) mặc định `LearnSpace` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   Đặt URI và App Password trong Render, không commit credentials vào repository. Không dùng `localhost` làm MongoDB host trên Render: trong container, `localhost` trỏ tới chính container ứng dụng.
3. Chọn **Create Web Service**. Container bind tới `0.0.0.0` và dùng biến `PORT` Render cung cấp (mặc định dự phòng `10000`).
4. Đảm bảo MongoDB cho phép kết nối mạng từ dịch vụ Render, sau đó mở URL được Render cấp.

**Lưu ý bảo mật:** ứng dụng hiện tự tạo tài khoản demo khi collection `Users` còn trống (`admin@lms.com` / `Admin@123`, cùng tài khoản Teacher/Student demo). Không để các thông tin này làm tài khoản production; trước khi public, hãy chuẩn bị tài khoản quản trị riêng và thay đổi hoặc loại bỏ credentials demo.

Tài liệu bài tập và tệp học viên nộp được lưu trong MongoDB GridFS, không lưu trên filesystem tạm của container Render. File upload giới hạn 25 MB mỗi tệp, tối đa 5 tệp cho mỗi lượt tạo bài tập/nộp bài.

## Tài khoản demo
- Admin: `admin@lms.com` / `Admin@123`
- Teacher: `teacher@lms.com` / `Teacher@123`
- Student: `student@lms.com` / `Student@123`

Các tài khoản demo chỉ dành cho môi trường local/dev; hãy thay đổi hoặc xóa chúng trước khi triển khai.
Tài khoản thành viên được tạo từ mục **Người dùng** bởi Admin; không có đăng ký công khai. Người dùng đã có thể tự đặt lại mật khẩu từ liên kết **Quên mật khẩu?** ở trang đăng nhập bằng OTP gửi đến email tài khoản. Mã có hiệu lực 10 phút, dùng một lần, giới hạn 5 lần thử và tối đa 5 email mỗi giờ (có khoảng nghỉ 60 giây giữa các lần gửi). Đặt lại mật khẩu sẽ hủy các phiên đăng nhập hiện tại của tài khoản đó.

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
- Quyền chức năng được lưu trong `Permissions` và `Users.PermissionCodes`; Admin có thể gán quyền hệ thống cho từng giáo viên/sinh viên, thêm/xóa mã quyền tùy chỉnh và ánh xạ một quyền tùy chỉnh vào một cặp MVC `Controller/Action`. Việc ẩn menu/nút chỉ là UX; policy/filter server-side vẫn chặn truy cập URL trực tiếp. Admin được bypass quyền chức năng, còn role vẫn xác định loại tài khoản và giới hạn các hành động miền (ví dụ nộp bài chỉ dành cho Student).
- Seed chạy migration bổ sung cho hồ sơ cũ chưa được khởi tạo: gán khoa/chương trình mặc định và liên kết lớp cũ với học kỳ phù hợp. Nếu đã có học kỳ nhưng không có học kỳ active, seed không tự kích hoạt lại học kỳ đã đóng; Admin cần chọn học kỳ đang mở.
- Ghi danh cập nhật `Classes.StudentIds` và `StudentRegistrations` bằng các thao tác MongoDB riêng, không phải transaction đa document; khi chạy MongoDB replica set có thể cân nhắc transaction nếu cần bảo đảm nguyên tử tuyệt đối.

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
