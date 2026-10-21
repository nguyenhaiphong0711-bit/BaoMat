# Sao lưu và phục hồi MongoDB

Hai script PowerShell đọc `MongoDb__ConnectionString` và `MongoDb__DatabaseName` từ biến môi trường hoặc `.env`. URI và mật khẩu không được in ra màn hình hay ghi vào file backup. Cần cài **MongoDB Database Tools** để có `mongodump` và `mongorestore` trong `PATH`.

Tạo bản sao lưu nén:

```powershell
.\scripts\backup-mongodb.ps1
```

File được tạo trong `backups/` với timestamp. Thư mục này bị Git bỏ qua. Nên lưu bản backup ở nơi được mã hóa và kiểm tra phục hồi định kỳ vào database thử nghiệm.

Phục hồi sẽ xóa rồi thay thế các collection trùng tên trong database được cấu hình. Script bắt buộc cờ xác nhận:

```powershell
.\scripts\restore-mongodb.ps1 -Archive .\backups\LMS_Production-20261022-120000.archive.gz -ConfirmRestore
```

Trước khi restore production, dừng ứng dụng để tránh ghi đồng thời, xác nhận đúng database trong `.env`, tạo thêm một backup hiện trạng và thử file trên database tạm. Atlas vẫn nên bật **Cloud Backup/PITR** nếu gói đang dùng hỗ trợ; script này là bản sao lưu chủ động cho bài tập và không thay thế chính sách sao lưu của nhà cung cấp.
