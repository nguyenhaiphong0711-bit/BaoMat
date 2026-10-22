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
