# Ma trận nghiệm thu hiện tại

Tài liệu này đối chiếu phạm vi bài tập với chức năng đã có trong nhánh chính. Các dòng “Đạt” đã được kiểm tra bằng bộ integration chạy trên ứng dụng thật và MongoDB tách biệt.

| Nhóm yêu cầu | Trạng thái | Bằng chứng chính |
|---|---|---|
| Tài khoản, đăng nhập, quên mật khẩu OTP | Đạt | BCrypt, khóa tài khoản, OTP một lần, giới hạn gửi/thử, hủy phiên cũ |
| Phân quyền Admin/Giảng viên/Sinh viên/Cán bộ | Đạt | Role + permission ở máy chủ, phạm vi lớp/khoa/chủ sở hữu, kiểm thử truy cập chéo |
| Khoa, ngành, môn, học kỳ, lớp và đăng ký | Đạt | CRUD có ràng buộc, duyệt đăng ký, sĩ số và kiểm tra trùng lịch |
| Bài học, tệp, bài tập, bài nộp và chấm điểm | Đạt | Giới hạn tệp, GridFS, thành viên lớp, trạng thái công bố |
| Lịch dạy và đề xuất mở lớp | Đạt | Giờ có thể dạy, duyệt đề xuất, tạo lớp đúng một lần |
| Học liệu dùng chung | Đạt | Phiên bản, kiểm duyệt khác tác giả, phạm vi môn/toàn trường |
| Ngân hàng câu hỏi và quiz | Đạt | Draft → duyệt → publish, lịch sử phiên bản, dấu AI, đáp án đúng phạm vi, attempt chống sửa đáp án |
| Hỗ trợ người học | Đạt | Phiếu theo khoa, hội thoại, phân công, timeline trạng thái và quyền riêng tư |
| Báo cáo và theo dõi tiến độ | Đạt | Báo cáo theo vai trò, CSV, sự kiện không lưu nội dung nhạy cảm |
| React + TypeScript | Đạt trong phạm vi bài tập | Context + reducer, lifecycle qua hooks, không cần Redux |
| Thông báo cá nhân | Đạt | Mở lớp, duyệt câu hỏi/học liệu và hỗ trợ; chỉ chủ tài khoản đọc/đánh dấu |
| AI hỗ trợ soạn câu hỏi | Đạt ở mức tùy chọn | Chỉ tạo bản nháp, giới hạn phản hồi, lưu provenance, kiểm tra schema/phạm vi môn, bắt buộc người dùng duyệt |
| Vận hành | Đạt ở mức bài tập | Liveness/readiness, backup/restore script, Docker/Render, smoke tải đồng thời |

Các hạng mục cần dịch vụ ngoài hoặc quy trình production vẫn phải nghiệm thu riêng trước khi public: SMTP thật, merchant VNPAY thật, nhà cung cấp AI thật, khôi phục một bản Atlas backup và kiểm thử tải theo quy mô người dùng dự kiến. MFA và xác minh email chưa nằm trong phạm vi đã triển khai.

