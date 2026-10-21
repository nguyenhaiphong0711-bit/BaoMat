# Kết quả rà soát dự án

Rà soát mã nguồn các nhóm MVC/API, xác thực/phân quyền, học vụ, lịch học, bài tập/tệp, học phí và cấu hình. Kiểm thử chạy trên Linux, .NET SDK 10.0.402, MongoDB 8.0 với database kiểm thử tách biệt. Phạm vi này không chứng minh mọi trường hợp nghiệp vụ hoặc tải đồng thời đều đúng.

## Đã sửa trong lượt này

- Gửi mail: chọn STARTTLS cho 587 và TLS ngay từ kết nối cho 465; giữ xác minh chứng chỉ. Chuẩn hóa khoảng trắng App Password của Gmail, có thời gian chờ tổng cho toàn bộ thao tác và nội dung text bên cạnh HTML.
- Trước đây `RequestCodeAsync` nuốt lỗi SMTP, còn `ForgotPassword` vẫn chuyển sang nhập OTP. Nay lỗi gửi/thiếu cấu hình trả HTTP 503 kèm thông báo tiếng Việt ngay tại trang quên mật khẩu.
- OTP chưa gửi không còn được sử dụng. Lượt gửi có trạng thái pending; khi SMTP thất bại thì xóa token mới hoặc khôi phục token đã gửi trước đó. Các thao tác đối chiếu hash để tránh ghi đè token của request mới.
- Giới hạn gửi lại có điều kiện cập nhật nguyên tử, tránh hai request cùng vượt qua cooldown. Lỗi QUIT sau khi máy chủ đã nhận thư không làm vô hiệu OTP.
- Tiêu thụ OTP đối chiếu đúng hash đã kiểm tra, tránh dùng kết quả xác minh token cũ để xóa token mới.
- Bổ sung kiểm thử tích hợp tự động và hướng dẫn cấu hình SMTP. Không ghi OTP hoặc mật khẩu SMTP vào log.

## Các vấn đề còn lại

| Mức độ | Nhóm | Bằng chứng và ảnh hưởng | Hướng xử lý |
|---|---|---|---|
| Cần cấu hình để gửi thật | Email | Máy hiện tại không có `.env` hay biến `Email__Username`/`Email__AppPassword`. Kiểm tra kết nối tới `smtp.gmail.com` ở cả 587/465 thất bại ngay tại DNS (`Temporary failure in name resolution`). | Nhập Gmail và App Password trong cấu hình bí mật của máy triển khai; cho phép DNS và SMTP outbound rồi thử nhận thư thật. Không nhập secret vào chat. |
| Cao | Xử lý lỗi production | `Program.cs` dùng `UseExceptionHandler("/Home/Error")`, nhưng không có `HomeController`; chỉ có `Views/Shared/Error.cshtml`. Đây là phát hiện tĩnh, chưa chạy mô phỏng lỗi production. | Tạo endpoint xử lý lỗi thật hoặc trỏ tới endpoint phù hợp, kiểm thử với Production. |
| Cao | Thư viện | Build/NuGet cảnh báo Snappier 1.0.0 (GHSA-pggp-6c3x-2xmx), SharpCompress 0.30.1 (GHSA-6c8g-7p36-r338), System.Security.Cryptography.Pkcs 10.0.0 (GHSA-2fmv-rv2c-59x7 và GHSA-846f-f764-76mx). | Nâng các dependency trực tiếp/transitive phù hợp rồi kiểm tra hồi quy MongoDB và MailKit. Chưa thay phiên bản ở lượt sửa email này. |
| Trung bình | Giới hạn OTP theo giờ | `PasswordResetTokens.ExpiresAt` có TTL 10 phút, trong khi bộ đếm gửi cùng document có cửa sổ 1 giờ. TTL xóa token hoặc đặt lại mật khẩu thành công sẽ xóa cả bộ đếm. Vì vậy giới hạn 5 email/giờ chưa được bảo đảm. Phát hiện từ mã/index, chưa kiểm thử theo thời gian thực 1 giờ. | Lưu lịch sử/rate limit độc lập với OTP; thêm giới hạn theo IP. |
| Trung bình | Đăng nhập đồng thời | `AuthService.LoginAsync` đọc rồi `ReplaceOneAsync` toàn bộ user. Các request đồng thời có thể ghi đè bộ đếm đăng nhập sai hoặc cập nhật user mới hơn. Đây là nguy cơ từ cách cập nhật, chưa stress test. | Dùng cập nhật các trường cần thiết và điều kiện nguyên tử/version. |
| Trung bình | Học vụ và hóa đơn | Duyệt/rút lớp cập nhật Classes, StudentRegistrations và TuitionInvoices bằng nhiều lệnh riêng. Lỗi giữa chừng có thể để lại trạng thái không đồng nhất. Có một số bước hoàn tác nhưng chưa phải transaction. | Transaction trên replica set hoặc quy trình phục hồi/idempotency có kiểm thử lỗi giữa chừng. |
| Trung bình | Bộ Postman chưa đồng bộ | Chạy bản collection nguyên gốc: 42 request, 19 test-script thất bại, số assertion được Newman đếm là 0 vì script đặt `pm.response...` bên ngoài `pm.test`. Lỗi gốc đầu tiên: collection gộp môn GEN sẵn có của giảng viên với môn thuộc khoa mới, trong khi `AssignTeachingSubjectsAsync` không cho môn từ nhiều khoa. Các bước tạo lớp/bài tập/nộp bài/lịch phía sau thất bại dây chuyền do thiếu ID. Không xem đây là 19 lỗi nghiệp vụ độc lập. | Chuẩn bị dữ liệu giảng viên/khoa nhất quán; bọc kiểm tra trong `pm.test` và dừng khi prerequisite thất bại. |
| Chưa cấu hình/kiểm thử ngoài hệ thống | VNPAY | Có ký HMAC, kiểm tra callback và xử lý hóa đơn; chưa có merchant/secret và callback HTTPS được kiểm thử với VNPAY thật. Không có luồng hoàn tiền tự động. | Kiểm thử sandbox với merchant và callback public hợp lệ trước khi dùng thanh toán. |
| Chưa triển khai đầy đủ | Điểm và tài khoản | Điểm đang là trung bình bài tập, chưa phải điểm tổng kết có trọng số; chưa có MFA/xác minh email. Seed tạo tài khoản demo nếu Users trống, kể cả khi không bật dữ liệu demo bổ sung. | Xác định yêu cầu điểm tổng kết; tách cấp tài khoản ban đầu cho production khỏi seed demo. |
| Giới hạn vận hành | Cookie | Runtime báo DataProtection key dùng bộ nhớ; phiên đăng nhập có thể hết hiệu lực sau restart. | Cấu hình kho key bền vững khi triển khai nhiều instance hoặc cần duy trì phiên. |

## Bằng chứng kiểm thử

- Build thành công; cảnh báo dependency còn tồn tại như trên.
- SMTP local thực có xác thực và chứng chỉ tin cậy tạm thời: gửi/đọc được OTP qua STARTTLS và SMTPS 465; đổi mật khẩu và đăng nhập mật khẩu mới; OTP dùng một lần; phiên cũ bị thu hồi.
- Kiểm thử lỗi SMTP authentication, timeout, QUIT sau DATA; token không gửi được được giải phóng và resend lỗi giữ mã cũ. Kiểm thử chứng chỉ không tin cậy bị từ chối, không tắt TLS verification.
- Kiểm thử cấu hình SMTP trống: tài khoản có thật và email không tồn tại đều trả 503, không sinh token.
- Tạo môn/lớp/bài học/bài tập, nộp bài, chấm điểm, chặn xóa môn đang dùng, tạo lịch và sinh viên ghi danh đều đạt với dữ liệu hợp lệ.
- 21 trang/API Admin trả 200; các trang chính Teacher/Student trả 200 và `/api/users` trả 403 cho hai vai trò này.
- Kiểm thử hiện tại chưa xác nhận thư đi đến hộp thư Gmail thật. SMTP local chỉ chứng minh đường gửi thư, xử lý lỗi và luồng OTP của ứng dụng.

Chạy lại theo phần kiểm thử hồi quy trong README. Kiểm thử dùng database `LMS_Reset_Test_<uuid>` rồi dọn database đó; không thay đổi database phát triển hiện có. Các vấn đề ngoài email được ghi nhận để xử lý riêng, tránh thay đổi hàng loạt nghiệp vụ trong cùng một bản sửa.
