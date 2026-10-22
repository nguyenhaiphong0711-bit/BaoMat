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

Quản trị học vụ bổ sung `AcademicPrograms`, `ProgramCourses`, `AcademicTerms` và `StudentRegistrations`. Mỗi hồ sơ sinh viên gắn một chương trình và học kỳ hiện tại; lớp được mở trong một học kỳ và có sức chứa. Khi sinh viên đăng ký, máy chủ kiểm tra đề cương và kỳ đăng ký, sau đó tạo yêu cầu chờ Admin duyệt; khi duyệt sẽ kiểm tra sĩ số và trùng lịch trước khi ghi danh. Admin có thể quản lý chương trình/đề cương, học kỳ, hồ sơ học vụ và quyền riêng từng tài khoản tại mục **Phân quyền**.

## Giao diện
- Bố cục điều hướng bên trái theo vai trò, thanh tiêu đề và vùng nội dung có chiều rộng nhất quán.
- Dashboard có lối tắt theo quyền; trang danh mục dùng bảng có thể cuộn trên màn hình nhỏ; biểu mẫu, trạng thái, thông báo và thẻ nội dung dùng chung hệ thống giao diện.
- CSS được gắn version tự động để trình duyệt tải giao diện mới sau khi build.
- Các trang danh sách hỗ trợ phân trang 10, 20 hoặc 50 dòng/trang và giữ bộ lọc hiện tại khi chuyển trang.

## Dữ liệu demo và phân trang
Dữ liệu minh họa chỉ được thêm khi chạy môi trường `Development` với `DemoData__Enabled=true`. Seeder tạo 15 khoa/viện, 25 ngành và khoảng 50 bản ghi có liên kết cho từng nhóm nghiệp vụ chính: môn học, giảng viên, sinh viên, lớp, bài học, bài tập, bài nộp, lịch, ghi danh, hóa đơn, học liệu, câu hỏi, bài kiểm tra, yêu cầu hỗ trợ và sự kiện học tập. Tên người và danh mục kinh tế - kinh doanh được viết gần với dữ liệu thực tế để dễ trình diễn, nhưng chỉ là dữ liệu tham khảo, không phải danh mục, biểu phí hay kết quả chính thức của Đại học Kinh tế Quốc dân.

Seeder dùng mã có tiền tố `DEMO`, email `@example.test`, không xóa dữ liệu cũ và có thể chạy lại mà không nhân bản. Các tài khoản bổ sung dùng mật khẩu `Demo@123`. Để nạp dữ liệu vào database Atlas đã khai báo trong `.env`, đặt `ASPNETCORE_ENVIRONMENT=Development` và `DemoData__Enabled=true`, chạy `dotnet run` một lần, dừng ứng dụng, sau đó đổi `DemoData__Enabled=false` và chạy lại. Không bật seeder khi vận hành Production. Seeder không tạo giả token khôi phục mật khẩu hoặc trạng thái thanh toán thành công.

## Chạy project
1. Cài .NET 10 SDK và Node.js 24 LTS (tối thiểu 22.12) để build/chạy giao diện React khi phát triển.
2. Cài MongoDB local và chạy MongoDB service.
3. Kiểm tra `appsettings.json`.
4. Để gửi mã khôi phục mật khẩu, sao chép `.env.example` thành `.env`, sau đó cấu hình Gmail SMTP bằng **Google App Password** (không dùng mật khẩu đăng nhập Google). Bật xác minh 2 bước cho tài khoản Google, tạo App Password và điền các giá trị trong `.env`:

   | Key | Value |
   |---|---|
   | `Email__Host` | `smtp.gmail.com` (mặc định) |
   | `Email__Port` | `587` (STARTTLS) hoặc `465` (TLS ngay khi kết nối) |
   | `Email__TimeoutSeconds` | `20`, cho phép từ 1 đến 120 giây |
   | `Email__Username` | Địa chỉ Gmail dùng gửi thư |
   | `Email__AppPassword` | Google App Password 16 ký tự |
   | `Email__SenderEmail` | (Không bắt buộc) mặc định dùng `Email__Username` |
   | `Email__SenderName` | (Không bắt buộc) mặc định `LearnSpace` |

   Ứng dụng tự đọc `.env` khi khởi động; biến môi trường đã được hệ điều hành/Render thiết lập sẽ được ưu tiên. Không đưa App Password vào `appsettings.json`, source control, ảnh chụp hoặc log. `.env` được loại trừ khỏi Git và Docker build; trên Render hãy khai báo cùng các biến này trong **Environment** của Web Service.

   Khi Google hiển thị App Password thành từng nhóm, ứng dụng tự bỏ khoảng trắng cho `smtp.gmail.com`. Nếu SMTP không xác thực được, TLS/DNS/kết nối lỗi hoặc quá thời gian chờ, trang quên mật khẩu trả lỗi 503 và cho phép thử lại; không chuyển sang màn hình nhập một OTP chưa gửi được. Mã gửi thành công trước đó được giữ lại nếu lượt gửi mới thất bại. Chỉ mã đã được SMTP chấp nhận và kích hoạt trong cơ sở dữ liệu mới được dùng để đặt lại mật khẩu. SMTP chấp nhận thư chưa bảo đảm thư vào Inbox; cần kiểm tra cả Spam và chính sách tài khoản gửi.

   Trên máy triển khai, phải có quyền kết nối outbound tới `smtp.gmail.com:587` hoặc `:465`; nếu nhà cung cấp chặn SMTP, cần cấu hình mạng phù hợp hoặc tích hợp dịch vụ email qua HTTPS. Không tắt kiểm tra chứng chỉ TLS. Nhập tài khoản gửi và App Password trong biến môi trường của máy chạy ứng dụng hoặc `.env` local được Git bỏ qua, rồi khởi động lại ứng dụng. Không gửi App Password trong chat hoặc commit vào Git.
5. Chạy:

```bash
dotnet restore
dotnet run
```

Mở `http://localhost:5000`.

## Giao diện React + TypeScript

Sau khi pull bản mới, từ thư mục dự án chỉ cần `dotnet run`: ứng dụng tự cài dependency frontend khi cần, build React và chạy Vite cùng ASP.NET. Mở `http://localhost:5000` để vào giao diện mới. Giữ nguyên MongoDB và `.env` hiện có. Máy phát triển cần Node.js; máy chạy bản publish không cần Node.js.

React hiện hỗ trợ đăng nhập/OTP, dashboard, môn học, lớp, bài học và tiến độ, bài tập, quiz, kho học liệu, hỗ trợ người học, báo cáo, đề xuất mở lớp và thông báo cá nhân. Ngân hàng câu hỏi có thể bật trợ lý AI để tạo bản nháp; xem [AI_QUESTIONS.md](AI_QUESTIONS.md). Các nghiệp vụ lịch/học vụ/quản trị còn lại dùng trang Razor hiện có, dùng chung phiên đăng nhập. Xem [FRONTEND.md](FRONTEND.md) về cấu trúc Context + reducer, [WORKFLOWS.md](WORKFLOWS.md) về quy trình nghiệp vụ và [ACCEPTANCE.md](ACCEPTANCE.md) về phạm vi đã nghiệm thu.

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

Hướng dẫn tạo backup và phục hồi MongoDB bằng MongoDB Database Tools nằm tại [OPERATIONS.md](OPERATIONS.md). Script đọc cấu hình hiện có trong `.env`, không lưu URI vào repository và bắt buộc xác nhận riêng trước khi restore.

1. Đẩy project lên GitHub và tạo một **Web Service** trên Render, chọn repository này và runtime **Docker**. Render sẽ dùng `Dockerfile` ở thư mục gốc.
2. Tạo MongoDB database có thể truy cập từ Render (ví dụ MongoDB Atlas), sau đó thêm các Environment Variables cho Web Service:

   | Key | Value |
   |---|---|
   | `MongoDb__ConnectionString` | MongoDB connection URI, e.g. the Atlas `mongodb+srv://...` URI |
   | `MongoDb__DatabaseName` | `LMS_Secure` hoặc tên database mong muốn |
   | `Email__Host` | `smtp.gmail.com` |
   | `Email__Port` | `587` |
   | `Email__Username` | Địa chỉ Gmail dùng gửi thư |
   | `Email__AppPassword` | Google App Password 16 ký tự |
   | `Email__SenderEmail` | (Không bắt buộc) mặc định dùng `Email__Username` |
   | `Email__SenderName` | (Không bắt buộc) mặc định `LearnSpace` |
   | `ASPNETCORE_ENVIRONMENT` | `Production` |

   Đặt URI và App Password trong Render, không commit credentials vào repository. Bắt buộc dùng MongoDB URI có thể truy cập từ Render; không dùng `localhost` vì trong container, `localhost` trỏ tới chính container ứng dụng. Ứng dụng không còn mặc định trỏ về `localhost`; nếu thiếu `MongoDb__ConnectionString`, sẽ báo rõ cấu hình còn thiếu thay vì thử kết nối sai địa chỉ.
3. Chọn **Create Web Service**. Container bind tới `0.0.0.0` và dùng biến `PORT` Render cung cấp (mặc định dự phòng `10000`).
4. Đảm bảo MongoDB cho phép kết nối mạng từ dịch vụ Render, sau đó mở URL được Render cấp.

**Lưu ý bảo mật:** tài khoản demo chỉ được tạo trong Development khi `Users` còn trống. Database Production trống yêu cầu cấu hình Admin ban đầu bằng `Bootstrap__AdminEmail` và `Bootstrap__AdminPassword`. Nếu đã có tài khoản demo từ bản cũ trên production, hãy đổi mật khẩu hoặc khóa chúng trước khi public.

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
- Đã có: BCrypt password hashing; lockout theo tài khoản sau số lần sai cấu hình; giới hạn endpoint đăng nhập/khôi phục theo IP; ghi log login; cookie `HttpOnly`, `SameSite=Lax`, hết hạn 8 giờ và sliding expiration; role authorization; kiểm tra membership/teacher ownership ở server; antiforgery cho form MVC; HSTS và cookie production chỉ gửi qua HTTPS; header chống nhúng trang, sniff MIME và vô hiệu camera/microphone/geolocation.
- Đã bổ sung: API login/me/logout; API trả status JSON `401`/`403`; mỗi request xác thực lại trạng thái active và role hiện tại để vô hiệu cookie cũ sau khi khóa tài khoản/đổi role; logout MVC chuyển sang POST có antiforgery (không còn logout bằng GET).
- Chưa đầy đủ cho production: mật khẩu mới bắt buộc tối thiểu 8 ký tự nhưng chưa có MFA, xác minh email hoặc giới hạn tốc độ theo IP; đã có đặt lại mật khẩu qua OTP email; lockout được cập nhật nguyên tử theo tài khoản để xử lý login đồng thời, nhưng chưa có giới hạn theo IP trên nhiều tài khoản. Thông báo lỗi đăng nhập đã dùng nội dung chung để hạn chế dò tài khoản. Hiện phù hợp demo/bài tập hơn là yêu cầu production có rủi ro cao.
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

## Kiểm thử hồi quy quên mật khẩu

`tests/password_reset_smoke.py` dùng ứng dụng thật, MongoDB và SMTP local có chứng chỉ TLS tin cậy tạm thời; không gửi email ra Internet. Cần Python 3, OpenSSL, Docker và container MongoDB local tên `baomat-mongo`. Mỗi lần chạy tạo database riêng rồi xóa database đó khi kết thúc.

```bash
python3 tests/password_reset_smoke.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
```

Thêm `--implicit-tls` để kiểm tra TLS trên cổng 465 (cổng phải đang trống), `--missing-config` để kiểm tra thiếu cấu hình, hoặc `--untrusted-tls` để xác nhận chứng chỉ không tin cậy bị từ chối. Tùy chọn `--postman` chạy thêm collection hiện có bằng Newman 6.2.3 và báo lỗi nếu script của collection thất bại. Xem `PROJECT_REVIEW.md` về các vấn đề còn tồn tại trong collection và phạm vi rà soát.

## Phân quyền và triển khai an toàn

Đã bổ sung [kho học liệu dùng chung](RESOURCES.md): nhiều môn dùng một tài liệu, metadata nguồn/giấy phép, phiên bản và gửi duyệt/phê duyệt/công bố. Tài khoản cũ cần Admin cấp các quyền kho mới trước khi dùng.

Đã bổ sung [cán bộ quản lý theo khoa](STAFF.md): Admin tạo tài khoản Staff và giao khoa; cán bộ dùng React để xem sinh viên và báo cáo học tập trong phạm vi được giao.

Xem [PERMISSIONS.md](PERMISSIONS.md) để biết ma trận 31 quyền, quy tắc quyền phụ thuộc và quá trình tự đồng bộ collection `Permissions`/quyền tài khoản khi khởi động. Bản cập nhật giữ quyền tùy chỉnh hợp lệ và quyền đã thu hồi, lưu đối chiếu trong `PermissionMigrations`.

Admin quản trị tài khoản/học vụ; Teacher chỉ quản lý lớp được phân công; Student chỉ nộp bài, xem điểm và học phí của chính mình. Quyền chức năng phải đồng thời phù hợp với role và quyền truy cập tài nguyên; gán nhầm quyền Teacher cho Student không cấp quyền chấm bài. Chi tiết phạm vi kiểm thử và các giới hạn còn lại ở [PROJECT_REVIEW.md](PROJECT_REVIEW.md).

Bộ kiểm thử bảo mật (dùng database local riêng và SMTP local, không gửi email thật):

```bash
python3 tests/security_regression.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll --postman
```

Database **Production trống** cần `Bootstrap__AdminEmail` và `Bootstrap__AdminPassword` (tối thiểu 12 ký tự) trong cấu hình bí mật của máy chạy ứng dụng. Các biến chỉ được đọc khi chưa có tài khoản; có thể gỡ sau khi tạo Admin. Không commit mật khẩu vào Git. Development vẫn có tài khoản demo; các tài khoản demo production đã tồn tại từ bản cũ cần được quản trị viên đổi mật khẩu hoặc khóa.

Bản sửa bổ sung unique index cho `Users.Email`. Nếu database cũ có email trùng, cần đối soát các tài khoản trùng trước khi ứng dụng khởi động được; không tự xóa dữ liệu. Giữ MongoDB authentication/TLS và giới hạn mạng khi triển khai. Các cập nhật học vụ xuyên nhiều collection vẫn cần transaction hoặc phục hồi nếu yêu cầu nguyên tử tuyệt đối.
