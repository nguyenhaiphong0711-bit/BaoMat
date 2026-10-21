# Rà soát phân quyền và bảo mật dữ liệu

## Phạm vi và kết quả

Đã sửa và chạy kiểm thử tích hợp với .NET 10, MongoDB local, SMTP TLS local và nhiều tài khoản độc lập: Admin, giáo viên phụ trách lớp, giáo viên khác, sinh viên trong lớp và sinh viên khác. Bộ kiểm thử sử dụng database riêng rồi dọn sau khi kết thúc. Không dùng database hay Gmail thật của người dùng.

## Các thay đổi

- **Quyền theo vai trò và phạm vi dữ liệu:** `ClassService.HasAccessAsync` chỉ cho giáo viên truy cập lớp đang được phân công, sinh viên truy cập lớp đang có tên trong danh sách và Admin truy cập theo quyền quản trị. Một ID nằm trong danh sách sinh viên cũ không biến thành quyền giáo viên sau khi đổi vai trò.
- **Tệp bài nộp:** sinh viên chỉ tải bài của chính mình trong lớp còn được tham gia. Chỉ Admin và giáo viên phụ trách có quyền chấm bài mới tải được bài người khác. Cấp nhầm `submissions.review` cho Student không mở đường tải tệp hoặc chấm điểm.
- **Thu hồi quyền:** cookie được đối chiếu với tài khoản ở mỗi request. Thu hồi quyền được áp dụng với cookie cũ; đổi vai trò, khóa tài khoản và đổi mật khẩu vô hiệu phiên tương ứng. Policy đồng thời đối chiếu quyền với vai trò; quyền tùy chỉnh chỉ bổ sung ràng buộc, không bỏ qua role/ownership.
- **Form phân quyền:** sửa binding tiền tố `Assignment.*` để thao tác lưu quyền Admin thật sự cập nhật dữ liệu. Chỉ hiển thị và chấp nhận quyền hệ thống phù hợp với Teacher/Student; kiểm tra trước khi cập nhật hồ sơ học vụ.
- **Tránh đè quyền khi đăng nhập:** bỏ ReplaceOne toàn bộ User; dùng compare-and-swap để chỉ cập nhật số lần đăng nhập sai và thời gian khóa. Không ghi đè mật khẩu, role hoặc PermissionCodes mới hơn. Request sai đồng thời vẫn kích hoạt khóa.
- **Bảo vệ dữ liệu trả về:** API lớp cho Student chỉ trả ID thành viên của chính họ; giao diện vẫn hiển thị tên bạn cùng lớp nhưng chỉ Admin/Teacher hoặc chính chủ được thấy email. Phản hồi của phiên xác thực đặt `Cache-Control: no-store`; bổ sung `nosniff` và referrer policy.
- **Email tài khoản:** thêm unique index `Users.Email` và xử lý lỗi trùng khi tạo tài khoản. Mọi đường tạo tài khoản của ứng dụng chuẩn hóa email thành chữ thường. Database cũ có email trùng phải được đối soát trước khi tạo được index; không tự xóa hay gộp tài khoản.
- **OTP:** lịch sử hạn mức được giữ riêng trong `PasswordResetRequestLimits`, không bị xóa khi dùng OTP hoặc khi TTL của OTP hết hạn. Có cooldown 60 giây và tối đa 5 lượt gửi thành công trong cửa sổ 1 giờ theo tài khoản; lỗi SMTP hoàn trả lượt đã giữ. Bộ đếm cũ được kế thừa khi còn token ở lần triển khai đầu.
- **Production:** thêm `/error` thật, trả lỗi chung và không lộ stack trace/configuration cho client. Database Production trống yêu cầu cấu hình Admin ban đầu; không tự tạo tài khoản demo có mật khẩu công khai. Các tài khoản demo đã tồn tại từ trước không bị tự động xóa.
- **Dependency:** nâng MongoDB.Driver lên 3.12.0 và System.Security.Cryptography.Pkcs lên 10.0.13. Restore/build hiện không có cảnh báo audit; Snappier resolve 1.3.1 và SharpCompress 0.48.1. Đây là kết quả audit tại lúc kiểm tra, không phải cam kết không còn mọi lỗ hổng.
- **Postman:** tạo giáo viên riêng cho mỗi lượt chạy; không gộp môn từ nhiều khoa cho cùng một giáo viên. Bao các assertion trong `pm.test` để Newman đếm được kết quả thật.

## Kiểm thử

Build Release thành công, không có lỗi hoặc cảnh báo. `tests/security_regression.py` mở rộng bộ OTP/nghiệp vụ và kiểm tra:

- Người không phải Admin bị chặn đọc tài khoản, log và tạo Admin.
- Giáo viên khác/sinh viên ngoài lớp không đọc bài, xem danh sách bài nộp hoặc chấm điểm của lớp.
- Tệp GridFS được tải lên qua luồng MVC thật chỉ được chính chủ và giáo viên được phân công đọc; sinh viên khác bị chặn kể cả có quyền Teacher gán nhầm.
- Admin thu hồi/cấp lại quyền bằng form thật; cookie giáo viên cũ phản ánh quyền ngay ở request tiếp theo.
- Đổi vai trò và khóa tài khoản vô hiệu cookie cũ; dữ liệu thành viên cũ không cấp quyền quản lý lớp.
- Nhiều request đăng nhập sai đồng thời khóa tài khoản đúng ngưỡng.
- Đặt lại mật khẩu nhiều lần không làm mất hạn mức OTP; email thứ sáu trong một giờ bị chặn.
- Production trả lỗi 500 chung qua handler thật; database Production trống chỉ tạo Admin được cấu hình, không tạo tài khoản demo.
- Bộ Postman có 43 request và 43 assertion; phải không có failure mới được xem là đạt.

Lệnh chạy:

```bash
python3 tests/security_regression.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll --postman
```

Cần các prerequisites trong README: Python, OpenSSL, Docker, MongoDB local tên `baomat-mongo`; `--postman` dùng Newman. Không chạy collection tạo dữ liệu trực tiếp trên database production.

## Còn giới hạn và cần cấu hình khi triển khai

- Kiểm thử không bao phủ mọi thứ tự request dưới tải lớn. Yêu cầu đã bắt đầu xử lý trước khi quyền bị thu hồi có thể hoàn tất; thu hồi áp dụng khi xác thực request tiếp theo.
- Duyệt/rút lớp và hóa đơn vẫn có các cập nhật riêng trên nhiều collection. Cần transaction với MongoDB replica set hoặc quy trình bù trừ/phục hồi được kiểm thử nếu yêu cầu tính nguyên tử xuyên collection.
- Chưa có MFA, xác minh email tài khoản, chống dò mật khẩu theo IP trên nhiều tài khoản, quét malware cho tệp hoặc hoàn tiền tự động. Chưa xác nhận callback VNPAY sandbox và thư Gmail thật trong môi trường người dùng.
- Cần kho DataProtection key bền vững được bảo vệ cho production/multiple instances; khóa trong bộ nhớ có thể làm mất phiên khi restart.
- Database production cần bật authentication, TLS, tài khoản ứng dụng giới hạn trong database cần thiết, giới hạn kết nối mạng và backup/restore. Không công khai MongoDB port cho Internet. Các điều này không được thiết lập tự động bởi thay đổi source.
- Nếu đã dùng tài khoản demo ở production từ bản trước, quản trị viên cần đổi mật khẩu hoặc khóa các tài khoản đó; bản sửa không xóa dữ liệu người dùng.

## Có cần chuyển sang SQL Server/SSMS?

Không cần đổi database chỉ để sửa các lỗi quyền trên. SSMS là công cụ quản trị SQL Server, không phải database. MongoDB và SQL Server đều có authentication/authorization, TLS và tùy chọn bảo vệ dữ liệu; lỗi ứng dụng cấp nhầm quyền hoặc bỏ qua quyền sở hữu vẫn có thể xảy ra ở cả hai. SQL Server có thể phù hợp nếu cần nhiều ràng buộc quan hệ và transaction xuyên bảng, nhưng chuyển đổi cần thiết kế/migration và kiểm thử riêng; bản sửa này giữ MongoDB và không di chuyển dữ liệu.
