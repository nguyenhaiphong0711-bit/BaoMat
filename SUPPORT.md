# Hỗ trợ người học

Mô-đun React TypeScript nằm tại `/app/support`. Sinh viên tạo yêu cầu với tiêu đề, nhóm vấn đề, mức ưu tiên và nội dung ban đầu. Khoa tiếp nhận luôn được lấy từ hồ sơ tài khoản ở máy chủ; các trường `StudentId`, `DepartmentId`, trạng thái hoặc người phụ trách do client tự gửi không được dùng.

Cán bộ chỉ xem được yêu cầu thuộc khoa đang được Admin giao và còn hoạt động. Khi cán bộ trả lời một yêu cầu chưa phân công, hệ thống tự phân công cho cán bộ đó và chuyển sang `InProgress`. Admin có thể chọn một cán bộ đang hoạt động thuộc đúng khoa. Cán bộ không thể phân công yêu cầu cho người khác hoặc người ngoài khoa.

Trạng thái gồm `Open`, `InProgress`, `Resolved`, `Closed`. Sinh viên trả lời một yêu cầu đã giải quyết sẽ mở lại thành `Open`. Yêu cầu đã đóng không nhận thêm tin nhắn. `Revision` được kiểm tra khi nhắn tin hoặc đổi trạng thái để tránh hai màn hình ghi đè nội dung của nhau. Mỗi yêu cầu giới hạn 200 tin nhắn và mỗi tin nhắn tối đa 5.000 ký tự.

Ba quyền mới:

- `support.view`: sinh viên xem yêu cầu của mình; cán bộ xem yêu cầu trong khoa;
- `support.create`: sinh viên tạo và trao đổi trong yêu cầu;
- `support.manage`: cán bộ tiếp nhận, trả lời, phân công và đổi trạng thái.

Với tài khoản cũ, Admin cần cấp `support.view` + `support.create` cho sinh viên và `support.view` + `support.manage` cho cán bộ. Admin có quyền toàn hệ thống nhưng vẫn phải chọn cán bộ thuộc đúng khoa khi phân công.

Phiên bản hiện tại hỗ trợ hội thoại văn bản và xử lý theo khoa. Chưa có tệp đính kèm, thông báo email, SLA, nhãn tùy chỉnh hoặc chuyển yêu cầu giữa các khoa.
