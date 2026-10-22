# Đề xuất mở lớp và thông báo

Giảng viên mở **Đề xuất mở lớp**, chọn một môn đã được Admin phân công, học kỳ còn hiệu lực, tên lớp và sức chứa. Mỗi giảng viên chỉ có một đề xuất đang chờ cho cùng môn và học kỳ. Việc gửi đề xuất chưa tạo lớp và chưa ghi danh sinh viên.

Admin xem cùng màn hình, nhập nhận xét rồi duyệt hoặc từ chối. Khi duyệt, máy chủ tạo lớp với đúng giảng viên, môn, học kỳ và sức chứa trong đề xuất. Quyết định chỉ được xử lý một lần; yêu cầu lặp lại bị từ chối. Lý do là bắt buộc khi từ chối.

Mục **Thông báo** chỉ trả dữ liệu của tài khoản đăng nhập. Admin nhận thông báo khi có đề xuất mới; giảng viên nhận kết quả xử lý và liên kết tới lớp vừa tạo. Người dùng có thể đánh dấu từng thông báo hoặc tất cả là đã đọc. API không nhận `userId` từ trình duyệt để thay đổi phạm vi.

Thông báo cũng bao phủ câu hỏi và học liệu chờ kiểm duyệt, kết quả duyệt, phiếu hỗ trợ mới, phản hồi và thay đổi trạng thái hỗ trợ. Thông báo kiểm duyệt được gửi cho Admin và cán bộ đang hoạt động thuộc đúng khoa; tác giả nhận kết quả. Nội dung thông báo hỗ trợ chỉ chứa tiêu đề phiếu, không sao chép hội thoại riêng tư.
