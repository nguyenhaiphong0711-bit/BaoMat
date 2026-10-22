# Báo cáo mục tiêu, kế hoạch và công nghệ triển khai LearnSpace

## 1. Mục tiêu ban đầu

Hệ thống hỗ trợ quản lý học tập, học liệu và tài nguyên số cho bốn nhóm Admin, cán bộ quản lý, giảng viên và sinh viên. Phạm vi được chia thành: quản lý học vụ/LMS; kho học liệu dùng chung/LCMS; ngân hàng câu hỏi và kiểm tra; theo dõi học tập; hỗ trợ người học; phân quyền, bảo mật và vận hành.

Kế hoạch thực hiện ban đầu gồm năm giai đoạn:

1. Chốt mô hình Subject–Program–Class và quyền theo Admin/Staff/Teacher/Student.
2. Xây kho học liệu nhiều môn, metadata, phiên bản, duyệt và công bố.
3. Xây ngân hàng câu hỏi, quiz và AI chỉ tạo bản nháp cần con người duyệt.
4. Xây ticket hỗ trợ, sự kiện học tập, báo cáo và phạm vi cán bộ theo khoa.
5. Hoàn thiện React, kiểm thử phân quyền/bảo mật, backup, vận hành và triển khai.

## 2. Công nghệ sử dụng

| Tầng | Công nghệ | Mục đích |
|---|---|---|
| Giao diện | React 19, TypeScript 7, React Router, CSS | SPA theo vai trò; TypeScript kiểm tra kiểu khi build |
| State | React Context + `useReducer`, state cục bộ | Phiên đăng nhập dùng chung; form/list dùng state gần component, phù hợp dự án cá nhân |
| Backend | ASP.NET Core .NET 10 MVC/Web API | Routing, validation, DI, middleware, authorization, rate limit và health check |
| Database | MongoDB + MongoDB.Driver 3.12 | Document data, typed filter, index và GridFS |
| File | MongoDB GridFS + SHA-256 | File ngoài webroot, ownership, tải có kiểm soát và chống trùng học liệu |
| Xác thực | Cookie Authentication, BCrypt.Net | HttpOnly cookie, lockout, hash có salt, lịch sử mật khẩu |
| Email | MailKit + Gmail SMTP TLS | Gửi OTP quên mật khẩu |
| Thanh toán | VNPAY HMAC-SHA512 | Tạo payment URL và xác minh callback/IPN |
| AI tùy chọn | HTTP API cấu hình bằng environment | Gợi ý câu hỏi ở trạng thái Draft; không tự duyệt |
| Kiểm thử | Python integration, Postman/Newman, Playwright | Test server/database/SMTP thật cục bộ và luồng React |
| Đóng gói | Docker, Vite build tích hợp MSBuild | Production chỉ cần ASP.NET runtime; không cần Node.js |

TypeScript làm giảm lỗi kiểu dữ liệu ở frontend, nhưng bảo mật vẫn do validation, xác thực và phân quyền ở backend quyết định.

## 3. Kết quả theo phân hệ

| Phân hệ | Chức năng đã triển khai | Cách thử nghiệm |
|---|---|---|
| Tài khoản | Login/logout, OTP, khóa tài khoản, đổi mật khẩu, lịch sử 5 hash, Admin quản lý tài khoản | SMTP TLS local; login đồng thời; cookie cũ sau đổi mật khẩu phải 401 |
| Phân quyền | 4 role, 31 permission, Staff theo khoa, Teacher theo lớp, Student theo enrollment | Gọi URL/API trực tiếp bằng nhiều role; đổi/thu hồi quyền khi cookie còn sống |
| Học vụ | Khoa, ngành, học kỳ, đề cương, môn, lớp, phân công, đăng ký và duyệt | Integration CRUD, ràng buộc khoa/môn/lớp, sức chứa và lịch trùng |
| Dạy và học | Bài học có file, bài tập, nộp bài, chấm điểm, hoàn thành bài | Student ngoài lớp bị 403; Teacher khác không xem/chấm; file bài nộp riêng tư |
| Lịch | Giờ có thể dạy, buổi học, đăng ký/hủy, kiểm tra trùng và sức chứa | Test thời gian, role, enrollment và lưu lịch sử khi hủy |
| Học liệu | Resource nhiều môn, metadata tác giả/nguồn/license, SHA-256 dedupe, version, review/publish/archive | Test hai môn dùng chung, quyền draft/published, reviewer và file ownership |
| Câu hỏi/quiz | Câu nhiều lựa chọn, version/review, AI draft, quiz, attempt và chấm tự động | Đáp án không trả trước khi nộp; draft chưa duyệt không đưa vào quiz |
| Hỗ trợ | Ticket, hội thoại, phân công Staff, trạng thái và lịch sử | Student chỉ ticket của mình; Staff chỉ khoa; Admin toàn hệ thống |
| Theo dõi/báo cáo | Learning event, tiến độ bài học, điểm, báo cáo scoped và CSV | So sánh Student/Teacher/Staff/Admin; tìm trường nhạy cảm trong JSON/export |
| Học phí | Hóa đơn từ đăng ký đã duyệt, snapshot đơn giá tín chỉ, VNPAY | Test chữ ký/số tiền/merchant/reference callback; không cấu hình thì từ chối an toàn |
| Vận hành | Health check, Docker, bootstrap Admin production, backup/restore script | Production smoke, React deep link/assets và request đồng thời |

## 4. Kiến trúc và luồng dữ liệu

React gọi JSON API bằng cookie cùng origin. Mỗi mutation của trình duyệt lấy antiforgery token mới. Controller chỉ nhận contract có DataAnnotations, sau đó service áp dụng quy tắc nghiệp vụ và typed MongoDB filter. Authorization gồm ba lớp: role, permission và phạm vi tài nguyên. Dữ liệu trả về dùng DTO; model chứa hash mật khẩu không được serialize thẳng.

File được đưa vào GridFS bằng ObjectId. Metadata lưu owner, class, purpose, content type chuẩn hóa và checksum khi là học liệu. Download luôn tìm metadata và kiểm tra quyền, không ánh xạ GridFS thành thư mục public.

## 5. State và lifecycle React

`AuthProvider` dùng Context + reducer cho ba trạng thái `loading`, `anonymous`, `authenticated`. `useEffect` tải phiên một lần và hủy cập nhật khi component unmount. Trang nghiệp vụ giữ filter/form/busy/error trong `useState`; dữ liệu bất đồng bộ đặt trong effect có cờ hủy hoặc hook dùng chung. Không cần Redux vì chưa có graph client state phức tạp, optimistic cache hoặc nhiều nguồn dữ liệu phải đồng bộ.

## 6. Bằng chứng kiểm thử

- `tests/security_regression.py`: authentication, IDOR, role/permission, CSRF, upload riêng tư, password history, NoSQL injection, production error.
- Các file `tests/*_integration.py`: học liệu, câu hỏi, quiz, Staff, support, báo cáo, workflow và học tập.
- `ClientApp/e2e/*.spec.ts`: giao diện và điều hướng theo role.
- `postman/LMS.postman_collection.json`: API CRUD và status code.
- `ACCEPTANCE.md`: danh sách nhóm hồi quy đã nghiệm thu.

Tại môi trường rà soát ngày 22/10/2026, TypeScript typecheck và Python syntax check đạt; `npm audit --omit=dev` báo 0 lỗ hổng. Môi trường này không có .NET SDK và Docker daemon, nên build/backend integration mới phải chạy lại tại máy có .NET 10, Docker/MongoDB theo lệnh trong README trước khi ghi số liệu nghiệm thu cuối.

## 7. Giới hạn còn lại

- Atlas security, audit và restore phải kiểm chứng trong tài khoản Atlas; source không thể tự bật thay người quản trị.
- VNPAY/Gmail/AI production cần credential sandbox/production của chủ tài khoản.
- MongoDB multi-document transaction chỉ nên bổ sung khi chạy replica set và nghiệp vụ yêu cầu nguyên tử tuyệt đối.
- AI là công cụ tạo nháp, không thay trách nhiệm kiểm duyệt nội dung.
- Nếu môn học bắt buộc SQL Server theo đúng cú pháp, cần nộp thêm lab SQL riêng như giải thích trong `SECURITY_REQUIREMENTS_REPORT.md`.
