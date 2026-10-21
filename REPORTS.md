# Sự kiện và báo cáo học tập

Trang `/app/reports` tính số liệu trực tiếp từ MongoDB theo tài khoản đăng nhập. API không nhận `studentId`, `teacherId` hoặc `departmentId` từ trình duyệt để mở rộng phạm vi.

- Sinh viên xem số lớp đang tham gia, bài tập đã công bố/đã nộp, điểm kiểm tra trung bình và yêu cầu hỗ trợ của mình.
- Giáo viên xem lớp phụ trách, bài tập, lượt nộp và điểm kiểm tra của các lớp đó.
- Cán bộ xem sinh viên, lớp, lượt kiểm tra và yêu cầu hỗ trợ thuộc khoa đang được giao.
- Admin xem số liệu tổng hợp toàn hệ thống.

Quyền `reports.view` cho phép mở báo cáo, nhưng phạm vi cuối cùng luôn bị giới hạn tiếp bởi vai trò và quan hệ lớp/khoa ở server. Khoa đã lưu trữ hoặc cán bộ chưa được gắn khoa sẽ không có báo cáo khoa.

Collection `LearningEvents` ghi các mốc có cấu trúc bắt đầu/nộp bài kiểm tra, tạo/trả lời/đổi trạng thái yêu cầu hỗ trợ. Sự kiện chỉ chứa loại, mã thực thể, phạm vi, thời gian và mô tả ngắn. Hệ thống không sao chép đáp án, nội dung hội thoại, mật khẩu, tệp hoặc nhận xét chấm bài vào sự kiện. Các sự kiện chỉ bắt đầu được ghi từ phiên bản này; dữ liệu cũ vẫn được tính trong chỉ số tổng hợp nhưng không có dòng thời gian hồi tố.

Với tài khoản cũ, Admin cấp `reports.view` cho người cần xem báo cáo. Tài khoản mới của Teacher, Student và Staff nhận quyền mặc định theo vai trò.

Phiên bản hiện tại hiển thị số liệu thời điểm hiện tại và 20 sự kiện gần nhất. Chưa có chọn khoảng ngày, xuất CSV/PDF, biểu đồ lịch sử, dữ liệu kho tổng hợp hoặc tác vụ lưu báo cáo định kỳ.
