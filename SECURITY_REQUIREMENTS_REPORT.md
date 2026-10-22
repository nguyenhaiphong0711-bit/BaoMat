# Báo cáo đáp ứng yêu cầu bảo mật

Ngày đối chiếu: 22/10/2026. Báo cáo này đối chiếu 30 tiêu chí được giao với mã nguồn ASP.NET Core/React/MongoDB. Trạng thái **Đạt** chỉ dùng khi có cơ chế trong mã và ca kiểm thử; **Đạt tương đương** là cùng mục tiêu bảo mật nhưng dùng cơ chế MongoDB thay cho tên tính năng SQL Server; **Cần cấu hình/kiểm chứng** là phần nằm ở MongoDB Atlas hoặc môi trường triển khai.

## Kết luận về SQL Server

Ứng dụng không cần đổi sang SQL Server để thực hiện CRUD hoặc bảo mật dữ liệu theo vai trò. MongoDB hỗ trợ tài khoản database, role, TLS, mã hóa at-rest, audit, index, backup và transaction trên replica set. SSMS chỉ là công cụ quản trị SQL Server, không tự làm ứng dụng an toàn hơn.

Nếu tiêu chí chấm bắt buộc phải có câu lệnh `CREATE SECURITY POLICY`, Dynamic Data Masking, SQL Server Audit, TDE và file `.bak`, các mục 1–8 không thể được tuyên bố đạt đúng công nghệ bằng MongoDB. Khi đó nên làm một bài lab SQL Server độc lập; không nên thay toàn bộ database của LMS chỉ để minh họa tám câu lệnh này.

## Đối chiếu 30 tiêu chí

| STT | Kết quả | Cách triển khai và bằng chứng kiểm thử |
|---:|---|---|
| 1 | Đạt tương đương ở ứng dụng; cần kiểm tra Atlas | Role Admin/Staff/Teacher/Student, 31 permission và phạm vi khoa/lớp được lưu trong MongoDB. `permission_integration.py` và `security_regression.py` thử gọi URL bằng role thấp. Atlas cần database user riêng cho ứng dụng với quyền `readWrite` đúng database, không dùng tài khoản owner. |
| 2 | Không áp dụng trực tiếp | MongoDB Driver dùng collection/filter có kiểu; API response DTO đóng vai trò giới hạn cột. MongoDB không dùng SQL stored procedure như đề mẫu. |
| 3 | Đạt tương đương ở server | Mọi truy vấn cá nhân gắn ID phiên, khoa, lớp hoặc phân công giáo viên. Test đổi ID xác nhận 403. Đây là application row scope, không phải SQL Server RLS. |
| 4 | Đạt tương đương | API dùng DTO và không serialize `PasswordHash`, lockout, lịch sử mật khẩu; báo cáo theo role chỉ trả trường được phép. MongoDB không có Dynamic Data Masking giống SQL Server. |
| 5 | Một phần | `ActivityLogs` ghi đăng nhập/đổi mật khẩu; `LearningEvents` ghi hoạt động học tập. MongoDB Atlas Audit Logs phải bật ở Atlas nếu tier hỗ trợ mới có log database độc lập. |
| 6 | Cần kiểm tra Atlas | URI Atlas dùng TLS. Encryption at rest do Atlas quản lý; cần chụp màn hình cấu hình cluster làm bằng chứng. Không có TDE của SQL Server trong source. |
| 7 | Một phần | Có `scripts/backup-mongodb.ps1` và `restore-mongodb.ps1`, cờ xác nhận restore, archive không vào Git. Cần bật Atlas Cloud Backup/PITR và thực hiện một lần restore vào database thử để nghiệm thu. |
| 8 | Đạt ở tài khoản ứng dụng; cần Atlas | Login sai được audit, cập nhật nguyên tử, khóa tài khoản và rate limit. Test tám request sai đồng thời. Atlas access history/alert cần cấu hình ngoài source. |
| 9 | Đạt tương đương | Cookie Authentication, BCrypt, lockout, role/policy và kiểm tra server. Không dùng ASP.NET Identity nhưng đạt hành vi bảo mật được yêu cầu. |
| 10 | Đạt tương đương NoSQL | MongoDB Driver expression/filter có kiểu, không nhận raw BSON từ client. Test payload email `{"$ne":null}` bị model validation từ chối. |
| 11 | Đạt | React escape dữ liệu mặc định, không chèn HTML người dùng. Playwright nhập `<script>` và xác nhận chỉ hiển thị dạng chữ. |
| 12 | Đạt | Mutation từ browser phải có antiforgery token; cookie HttpOnly/SameSite/Secure theo môi trường. Test POST không token bị từ chối và dữ liệu không đổi. |
| 13 | Đạt | Secret đọc từ environment/`.env`, `.env` và backup bị Git ignore, `.env.example` chỉ có placeholder. Production không mặc định connection string local. |
| 14 | Đạt | Tối đa 25 MB/5 tệp, allowlist đuôi, kiểm tra magic bytes, `Path.GetFileName`, GridFS ObjectId, file ngoài webroot và kiểm tra owner/class. Test file thực thi đổi tên `.pdf` bị 400. |
| 15 | Đạt phần lớn | `ILogger`, ActivityLog, request ID, error handler production và health check. Response 500 không có stack trace/secret. Atlas/APM vẫn cần cấu hình nếu muốn cảnh báo vận hành tập trung. |
| 16 | Đạt | Menu React dùng quyền để hiển thị; action/filter/policy kiểm tra lại trên server. Cookie được đối chiếu database mỗi request nên khóa, đổi role/quyền/mật khẩu có hiệu lực với phiên cũ. |
| 17 | Đạt | BCrypt tự tạo salt; mật khẩu mạnh; đổi mật khẩu yêu cầu mật khẩu hiện tại; giữ 5 hash cũ để chống tái sử dụng; audit không chứa mật khẩu; đổi thành công buộc đăng nhập lại. |
| 18 | Đạt | Student chỉ xem dữ liệu của mình, Teacher chỉ lớp được phân công, Staff chỉ khoa được giao. Integration test thay ObjectId và thử quyền legacy sai. |
| 19 | Đạt phần lớn | Input có kiểu/độ dài, ObjectId được parse, danh sách có page size allowlist 10/20/50. Một số catalog nhỏ còn lọc trong memory; không tạo injection nhưng cần tối ưu nếu dữ liệu rất lớn. |
| 20 | Một phần | Có ActivityLog và LearningEvent ở tầng ứng dụng. Cần bật Atlas Database Auditing để có tầng log database thứ hai và đối chiếu theo request/time/user. |
| 21 | Đạt | API báo cáo theo phạm vi role; test xác nhận không có password, đáp án quiz, tin hỗ trợ hoặc nhận xét ngoài phạm vi trong JSON/export. |
| 22 | Đạt | API trả 401 khi chưa đăng nhập, 403 khi thiếu quyền, validation trả 400 và kiểm tra ownership trên tài nguyên. Postman và integration test chạy bằng nhiều role. |
| 23 | Đạt | Production dùng `/error`, Problem Details chung và `X-Request-Id`; test gây lỗi VNPAY xác nhận không lộ exception/secret. Chi tiết vẫn được ghi server log. |
| 24 | Một phần | `/health/live`, `/health/ready`, script backup/restore và hướng dẫn sự cố đã có. Chưa có bằng chứng restore Atlas thật vì đây là thao tác môi trường. |
| 25 | Đạt | Secret scan, role/ownership review, production error test và các lỗi/cách sửa được ghi trong `PROJECT_REVIEW.md` cùng báo cáo này. |
| 26 | Đạt ở phạm vi bài tập | Rate limit login/OTP, pagination, timeout Mongo/SMTP, health check và smoke 96 request đồng thời. Chưa phải benchmark production hoặc giám sát CPU/RAM dài hạn. |
| 27 | Đạt phần lớn | Có unique/compound/sort indexes cho email, đăng ký, tiến độ, quiz, ticket, event, notification, học kỳ và log. Cần chạy `explain("executionStats")` trên dữ liệu production nếu muốn số đo trước/sau. |
| 28 | Đạt cho frontend; backend cần chạy lại tại máy có SDK | `npm audit --omit=dev` ngày 22/10/2026: 0 lỗ hổng trên 77 dependency. NuGet đã nâng MongoDB.Driver 3.12.0 và Pkcs 10.0.13; cần lưu output `dotnet list package --vulnerable --include-transitive` khi nghiệm thu. |
| 29 | Đạt | SMTP bắt buộc TLS và kiểm tra certificate; cookie/antiforgery/header được siết; log không chứa OTP/password/URI; upload không tin MIME/tên client. Test certificate không tin cậy xác nhận bị từ chối. |
| 30 | Đạt | Test file rỗng/sai ID/sai owner/quá phạm vi, nội dung giả PDF, file riêng tư và đường dẫn đoán. GridFS không cho chạy file như static asset và không ghi đè theo tên. Antivirus chuyên dụng là phần mở rộng production. |

## Phần code mới cho tiêu chí 17

- `Models/User.cs`: `PasswordHistoryHashes` chỉ chứa tối đa năm BCrypt hash, không lưu mật khẩu rõ.
- `UserService.ChangePasswordAsync`: xác minh mật khẩu hiện tại, kiểm tra lịch sử, update theo điều kiện hash hiện tại để tránh ghi đè đồng thời, đặt lại lockout.
- `POST /api/frontend/change-password`: validation độ phức tạp, antiforgery, audit và sign-out sau thành công.
- `ClientApp/src/AuthPages.tsx`: form React kiểm tra xác nhận và hiển thị lỗi an toàn.
- `PasswordResetService`: khôi phục bằng OTP cũng không được dùng lại mật khẩu hiện tại/gần đây.

Ca kiểm thử tự động tạo tài khoản riêng, thử sai mật khẩu hiện tại, đổi thành công, kiểm tra cookie cũ bị 401, thử quay lại mật khẩu cũ bị 400, kiểm tra lịch sử có một hash và ActivityLog có đủ ba sự kiện.

## Phần code mới cho tiêu chí 14 và 30

`FileStorageService.HasExpectedSignatureAsync` đọc tối đa 4 KB đầu tệp và kiểm tra signature PDF, PNG, JPEG, ZIP/Office Open XML, Office cũ; TXT/CSV không được có byte NUL. Ca kiểm thử gửi nội dung bắt đầu bằng `MZ` nhưng đặt tên `malware.pdf` và MIME `application/pdf`; endpoint phải trả 400.

## Việc phải làm trên MongoDB Atlas khi nghiệm thu

1. Tạo database user riêng cho ứng dụng, chỉ cấp quyền trên database LMS; đổi mật khẩu đã từng chia sẻ và không dùng Atlas owner trong URI ứng dụng.
2. Giới hạn Network Access theo IP của Render/máy quản trị; không giữ `0.0.0.0/0` khi public.
3. Xác nhận TLS và Encryption at Rest trong màn hình Security.
4. Bật Cloud Backup/PITR nếu tier hỗ trợ; restore một snapshot vào database thử rồi chạy smoke test.
5. Bật Database Auditing/Alerts nếu tier hỗ trợ; lưu bằng chứng login lỗi và thao tác ghi.

Các bước trên không thể hoàn thành hoặc chứng minh chỉ bằng commit source vì chúng thuộc tài khoản Atlas và gói dịch vụ.
