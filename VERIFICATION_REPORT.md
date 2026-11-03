# Báo cáo kiểm chứng dự án LearnSpace

Ngày kiểm chứng: 03/11/2026.

## Kết quả

| Hạng mục | Kết quả | Bằng chứng |
|---|---|---|
| Backend .NET | Đạt | `dotnet build LMS.csproj`: 0 lỗi |
| React TypeScript | Đạt | `npm run build`: typecheck và Vite production build thành công |
| Regression backend | Đạt | `tests/reports_integration.py`: 64 nhóm kiểm thử đạt |
| React E2E | Đạt | Playwright Chromium: 20/20 ca đạt |
| Backup/restore MongoDB | Đạt trên database thử | `mongodump` lưu 2 Users + 1 Event; `mongorestore` khôi phục 3/3 document, 0 lỗi; giá trị kiểm tra trước/sau bằng nhau |
| Dependency frontend | Đạt | `npm audit --omit=dev`: 0 lỗ hổng |
| Dependency backend | Đạt | `dotnet list package --vulnerable --include-transitive`: không có package dễ bị tổn thương theo nguồn NuGet hiện tại |

## Phạm vi kiểm thử bảo mật

Bộ regression kiểm tra đăng nhập và OTP, khóa tài khoản, đổi mật khẩu, vô hiệu phiên cũ, role/permission, phạm vi khoa/lớp, IDOR, CSRF, NoSQL injection, upload giả mạo, quyền file, báo cáo theo vai trò, dữ liệu quiz, support ticket, rate limiting, lỗi production, health check và bootstrap Admin.

Playwright kiểm tra luồng React cho bài học/bài tập, phân quyền, ngân hàng câu hỏi, quiz, báo cáo, học liệu, Staff, hỗ trợ người học, đề xuất mở lớp, đăng nhập, CSRF, giao diện mobile và upload.

## Lỗi phát hiện và đã sửa

Cookie tạo ngay sau khi đổi mật khẩu đôi lúc bị xem là cookie cũ vì thời điểm trong cookie và MongoDB có độ chính xác khác nhau. Hệ thống nay đưa `lms:password-version` vào cookie đã ký và đối chiếu chính xác với `PasswordChangedAt`. Cookie cũ bị vô hiệu, còn đăng nhập ngay bằng mật khẩu mới hoạt động ổn định.

Bộ test cũng được sửa để gửi CSRF token thật cho API đổi mật khẩu, sử dụng mật khẩu fixture hiện tại, chỉ chạy E2E khi đúng fixture phân hệ và không tiêu thụ quota đăng nhập trong smoke đọc đồng thời.

## Backup/restore đã kiểm chứng

Việc kiểm chứng dùng hai database cục bộ tạm thời, không truy cập hoặc thay đổi Atlas production:

1. Tạo `LMS_Backup_Verification` có 2 document Users và 1 document Events.
2. Tạo archive gzip bằng `mongodump`.
3. Restore có `--drop` và ánh xạ namespace sang `LMS_Backup_Restore_Verification`.
4. So sánh số document và giá trị sentinel `42`: hoàn toàn trùng khớp.
5. Xóa cả hai database thử sau khi hoàn tất.

Các script dành cho người dùng nằm tại `scripts/backup-mongodb.ps1` và `scripts/restore-mongodb.ps1`. Restore mặc định sang database thử và yêu cầu cờ `-ConfirmRestore` để giảm nguy cơ ghi đè nhầm.

## Phần chưa thể chứng minh bằng source/local test

- Atlas Audit Logs, Alerts, Encryption at Rest và Cloud Backup/PITR phải được bật và chụp bằng chứng trong Atlas.
- Restore Atlas thật cần thực hiện vào database thử trên Atlas, tuyệt đối không restore đè production để trình diễn.
- Các tính năng SQL Server như RLS, Dynamic Data Masking, TDE, SQL Server Audit và stored procedure cần một lab SQL Server riêng nếu đề chấm bắt buộc đúng công nghệ.
