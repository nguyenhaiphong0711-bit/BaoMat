# Bài kiểm tra và chấm điểm tự động

Mô-đun nằm tại `/app/quizzes`. Giáo viên tạo bài kiểm tra cho lớp mình phụ trách và chỉ chọn được câu hỏi đã công bố thuộc đúng môn của lớp. Bài nháp không hiển thị cho sinh viên. Sau khi công bố, sinh viên thuộc danh sách lớp có thể bắt đầu trong khoảng thời gian mở/đóng đã cấu hình.

Mỗi sinh viên có một lượt làm. Khi bắt đầu, máy chủ chụp nội dung, lựa chọn, đáp án và giải thích của các câu hỏi vào lượt làm; thay đổi trong ngân hàng về sau không làm đổi kết quả cũ. Thời điểm hết hạn là thời điểm sớm hơn giữa giờ đóng bài và `StartedAt + DurationMinutes`.

Trước khi nộp, API dành cho sinh viên chỉ trả nội dung và lựa chọn; `CorrectOption` là `null` và phần giải thích rỗng. Máy chủ kiểm tra tất cả chỉ số đáp án và tính điểm theo thang 10. Lần nộp tiếp theo trả lại kết quả đầu tiên, không chấm lại. Sau khi nộp, sinh viên được xem đáp án và giải thích; giáo viên xem danh sách người làm, thời điểm nộp và điểm.

Ba quyền của mô-đun:

- `quizzes.view`: xem bài kiểm tra trong lớp;
- `quizzes.manage`: giáo viên tạo, sửa bản nháp, công bố, lưu trữ và xem kết quả;
- `quizzes.attempt`: sinh viên bắt đầu và nộp bài.

Với tài khoản cũ, Admin cần cấp `quizzes.view` + `quizzes.manage` cho giáo viên và `quizzes.view` + `quizzes.attempt` cho sinh viên. Tài khoản mới nhận quyền mặc định theo vai trò. Cả route API, quan hệ lớp và quyền động đều được kiểm tra; gán chuỗi quyền trực tiếp trong MongoDB không thể biến sinh viên thành giáo viên.

Phiên bản hiện tại hỗ trợ trắc nghiệm một đáp án, một lượt làm, tự chấm và kết quả theo lớp. Chưa có trộn thứ tự câu, nhiều lượt làm, mật khẩu đề, giám sát thi, câu tự luận hoặc phân tích độ khó câu hỏi.
