# AI hỗ trợ tạo bản nháp câu hỏi

Tính năng này là tùy chọn. Giảng viên chọn môn mình được phân công, nhập chủ đề, độ khó và mục tiêu học tập; AI tạo một câu hỏi trắc nghiệm để giảng viên sửa. Kết quả luôn ở trạng thái `Draft`, vẫn phải gửi duyệt và được một người đủ quyền khác phê duyệt trước khi công bố.

Khai báo nhà cung cấp tương thích OpenAI Chat Completions trong `.env` local hoặc Environment của hosting:

```dotenv
Ai__Enabled=true
Ai__Endpoint=https://api.openai.com/v1/chat/completions
Ai__ApiKey=your-secret-key
Ai__Model=your-model-name
Ai__TimeoutSeconds=30
```

Endpoint bắt buộc dùng HTTPS. Khóa API không được gửi xuống trình duyệt, lưu vào MongoDB hay commit. Máy chủ yêu cầu JSON gồm `content`, bốn phần tử `options`, `correctOption` từ 0 đến 3 và `explanation`; phản hồi sai cấu trúc hoặc lớn hơn 64 KiB bị loại bỏ và không tạo câu hỏi. Bản ghi lưu model, thời điểm AI hỗ trợ và tiếp tục giữ lịch sử khi giảng viên chỉnh sửa. Khi chưa cấu hình hoặc nhà cung cấp lỗi/quá thời gian, API trả 503 và người dùng vẫn có thể tạo câu hỏi thủ công.

