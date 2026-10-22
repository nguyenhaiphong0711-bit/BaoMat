# AI hỗ trợ tạo bản nháp câu hỏi

Tính năng này là tùy chọn. Giảng viên chọn môn mình được phân công, nhập chủ đề, độ khó và mục tiêu học tập; AI tạo một câu hỏi trắc nghiệm để giảng viên sửa. Kết quả luôn ở trạng thái `Draft`, vẫn phải gửi duyệt và được một người đủ quyền khác phê duyệt trước khi công bố.

Tích hợp mặc định dùng **OpenAI Responses API** và Structured Outputs. Tạo API key trong OpenAI Platform, nạp phương thức thanh toán/API credit nếu tài khoản yêu cầu, rồi khai báo trong `.env` local hoặc Environment của hosting. Gói ChatGPT trên giao diện chat và phí OpenAI API là hai dịch vụ thanh toán riêng.

```dotenv
Ai__Enabled=true
Ai__Endpoint=https://api.openai.com/v1/responses
Ai__ApiKey=your-secret-key
Ai__Model=gpt-4.1-mini
Ai__TimeoutSeconds=30
```

Endpoint bắt buộc dùng HTTPS. Khóa API chỉ được máy chủ đọc, không được gửi xuống trình duyệt, lưu vào MongoDB, log hay commit. Không đặt khóa trong `appsettings.json`. Khi deploy Render, tạo năm biến trên trong mục **Environment** rồi restart service.

Máy chủ gửi sang OpenAI tên môn, chủ đề, độ khó và mục tiêu học tập; không gửi hồ sơ người dùng, email, bài làm hay dữ liệu sinh viên. Structured Outputs buộc phản hồi theo schema gồm `content`, đúng bốn `options`, `correctOption` từ 0 đến 3 và `explanation`. Phản hồi sai cấu trúc hoặc lớn hơn 64 KiB bị loại bỏ và không tạo câu hỏi. Endpoint bị giới hạn 10 request/phút theo IP để kiểm soát chi phí.

Bản ghi luôn được tạo ở trạng thái `Draft`, lưu model/thời điểm AI hỗ trợ và một sự kiện audit `question.ai-draft`. Giảng viên phải đọc, chỉnh sửa, gửi duyệt; một cán bộ khác mới được duyệt và sau đó tác giả mới có thể công bố. Khi chưa cấu hình, hết credit, OpenAI lỗi hoặc quá thời gian, API trả 503 và người dùng vẫn có thể tạo câu hỏi thủ công. Cấu hình Chat Completions cũ vẫn được parser hỗ trợ để chuyển đổi thuận lợi, nhưng cấu hình mới nên dùng `/v1/responses`.

Ca integration dùng HTTPS OpenAI giả lập, xác nhận request có JSON Schema, câu hỏi được lưu nháp và audit được ghi. Test không gọi API thật nên không tốn token; lần nghiệm thu thật cần tạo một câu nháp bằng API key riêng và kiểm tra usage trên OpenAI Platform.

