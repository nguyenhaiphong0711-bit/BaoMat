# Ngân hàng câu hỏi trắc nghiệm

Mô-đun nằm tại `/app/questions` và dùng React TypeScript. Giáo viên soạn câu hỏi cho môn đang được phân công; cán bộ chỉ duyệt câu hỏi thuộc khoa đang được Admin giao. Admin có thể quản lý và duyệt trong toàn trường, nhưng người tạo hoặc người sửa gần nhất không thể tự duyệt.

Mỗi câu hỏi có môn học, chủ đề, độ khó, nội dung, từ 2 đến 6 lựa chọn khác nhau, đúng một đáp án và phần giải thích. Luồng trạng thái là `Draft → PendingReview → Approved → Published`. Người duyệt có thể chuyển câu hỏi sang `Rejected` kèm nhận xét; giáo viên sửa lại sẽ tạo bản nháp để gửi duyệt lại. Việc công bố là thao tác riêng của người quản lý câu hỏi sau khi duyệt.

Ba quyền mới là:

- `questions.view`: xem ngân hàng câu hỏi;
- `questions.manage`: giáo viên soạn, gửi duyệt, công bố và lưu trữ câu hỏi của mình;
- `questions.review`: cán bộ duyệt câu hỏi trong khoa.

Sinh viên bị chặn ở cả route API và giao diện để đáp án đúng không bị lộ. Câu hỏi nháp chỉ người sở hữu/Admin thấy; cán bộ chỉ thấy câu đã gửi trong khoa; giáo viên khác chỉ thấy câu đã công bố thuộc môn mình được phân công. Tất cả thao tác ghi đều kiểm tra CSRF và `Revision` để từ chối cập nhật từ màn hình đã cũ.

Sau khi pull, chạy `dotnet run`; hệ thống tự thêm định nghĩa quyền. Với tài khoản đã tồn tại, Admin cần vào **Phân quyền** để cấp `questions.view` + `questions.manage` cho giáo viên và `questions.view` + `questions.review` cho cán bộ phù hợp. Hệ thống không tự cấp lại quyền đã bị thu hồi.

Phiên bản này là ngân hàng câu hỏi thủ công có kiểm duyệt. Chưa có đề thi, lần làm bài, chấm điểm tự động, nhập hàng loạt hoặc sinh câu hỏi bằng AI. Phần AI sẽ chỉ tạo bản nháp và vẫn phải qua người duyệt khi được bổ sung ở giai đoạn sau.
