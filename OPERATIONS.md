# Sao lưu và phục hồi MongoDB

Hai script PowerShell đọc `MongoDb__ConnectionString` và `MongoDb__DatabaseName` từ biến môi trường hoặc `.env`. URI và mật khẩu không được in ra màn hình hay ghi vào file backup. Cần cài **MongoDB Database Tools** để có `mongodump` và `mongorestore` trong `PATH`.

Tạo bản sao lưu nén:

```powershell
.\scripts\backup-mongodb.ps1
```

File được tạo trong `backups/` với timestamp. Thư mục này bị Git bỏ qua. Nên lưu bản backup ở nơi được mã hóa và kiểm tra phục hồi định kỳ vào database thử nghiệm.

Phục hồi mặc định đổi namespace sang database thử nghiệm `<database>-RestoreTest`, vì vậy không ghi đè database ứng dụng. Script bắt buộc cờ xác nhận:

```powershell
.\scripts\restore-mongodb.ps1 -Archive .\backups\LMS_Production-20261022-120000.archive.gz -ConfirmRestore
```

Có thể đặt tên database kiểm thử riêng bằng `-TargetDatabase LMS_Restore_20261022`. Chỉ khi đã dừng ứng dụng và có chủ ý phục hồi đè database cấu hình mới dùng thêm `-TargetDatabase LMS_Production -AllowConfiguredDatabase`.

Trước khi restore production, dừng ứng dụng để tránh ghi đồng thời, xác nhận đúng database trong `.env`, tạo thêm một backup hiện trạng và thử file trên database tạm. Atlas vẫn nên bật **Cloud Backup/PITR** nếu gói đang dùng hỗ trợ; script này là bản sao lưu chủ động cho bài tập và không thay thế chính sách sao lưu của nhà cung cấp.

## Kiểm tra trạng thái dịch vụ

- `GET /health/live` trả 200 khi tiến trình web đang chạy.
- `GET /health/ready` chỉ trả 200 khi ứng dụng ping được MongoDB; dùng endpoint này cho health check của Render hoặc container orchestrator.

Sau khi build Release, bộ hồi quy đầy đủ kiểm tra cả hai endpoint và chạy 96 lượt đọc đồng thời trên health, lớp học và báo cáo:

```bash
python3 tests/reports_integration.py --dotnet "$(command -v dotnet)" --dll path/to/LMS.dll --postman
```

Đây là smoke test để phát hiện lỗi kết nối, phân quyền và lỗi 5xx dưới tải nhỏ. Trước khi phục vụ thật, cần chạy load test riêng với số người dùng, dữ liệu, vùng mạng và Atlas tier dự kiến.

## Bổ sung quiz minh họa trên production

Khi production đã có lớp và môn học nhưng trang quiz còn trống, dùng script PowerShell để tạo dữ liệu qua API nghiệp vụ. Script không kết nối trực tiếp MongoDB, không ghi đè quiz hiện có và chỉ chọn lớp chưa có quiz:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\populate-production-quizzes.ps1 -Apply
```

Script yêu cầu mật khẩu Admin tại thời điểm chạy, không lưu mật khẩu vào file. Với mỗi lớp được chọn, script tạo câu hỏi, gửi qua một tài khoản reviewer tạm, phê duyệt, công bố, tạo quiz và công bố quiz. Tài khoản reviewer tạm bị khóa trong khối `finally` kể cả khi một bước sau đó lỗi.

Mặc định tạo tối đa 3 quiz. Có thể đặt từ 1 đến 5:

```powershell
.\scripts\populate-production-quizzes.ps1 -Apply -Count 5
```

Bản Python có cùng mục đích dành cho Linux/macOS:

```bash
python3 scripts/populate-production-quizzes.py --apply --count 3
```

Sau khi chạy, đăng nhập Teacher/Admin để kiểm tra danh sách quiz và đăng nhập Student thuộc lớp để xác nhận chỉ quiz đã công bố mới xuất hiện. Không bật `DemoData__Enabled` trên Production chỉ để bổ sung một vài quiz.

Nếu cần ghi thẳng Atlas, dùng migration mongosh một lần thay cho API:

```powershell
mongosh $env:MongoDb__ConnectionString scripts/fill-atlas-quizzes.mongodb.js
```

`fill-atlas-quizzes.mongodb.js` không được gọi khi ứng dụng khởi động và không thuộc `DemoDataSeeder`. Script từ chối chạy trên database hệ thống, chỉ chọn tối đa 5 lớp đang hoạt động chưa có quiz, tái sử dụng giáo viên/môn hiện có, tạo ba câu hỏi Published có thông tin reviewer, tạo quiz Published và ghi `LearningEvent`. Chạy lại không tạo thêm quiz cho lớp đã có quiz.
