# Cán bộ quản lý theo khoa

Vai trò `Staff` bổ sung phân hệ theo dõi người học ở mức đọc dữ liệu. Bộ quyền hệ thống hiện có 18 mã; hai mã mới là `staff.students.view` và `staff.progress.view` (cần `staff.students.view`).

## Sử dụng

1. Admin vào **Người dùng → Tạo tài khoản**, chọn **Cán bộ quản lý**.
2. Vào **Phân quyền / hồ sơ** của tài khoản đó, chọn **Khoa được giao quản lý** và các quyền cần cấp, rồi lưu.
3. Cán bộ đăng nhập giao diện React, chọn **Sinh viên trong khoa**. Có tìm kiếm tên/email, phân trang và xem tiến trình từng sinh viên.
4. Bỏ khoa được phân công hoặc thu hồi quyền trong màn hình Admin để ngừng quyền truy cập tương ứng. Cán bộ mới chưa có khoa sẽ không xem được danh sách, kể cả đã có hai quyền chức năng.

Mỗi cán bộ hiện phụ trách một khoa. Chưa hỗ trợ nhiều khoa hoặc giới hạn nhỏ hơn theo chương trình/danh sách phân công.

## Phạm vi dữ liệu và báo cáo

- Danh sách lấy theo DepartmentId của tài khoản cán bộ đang đăng nhập, không theo departmentId do trình duyệt gửi lên. Khoa phải còn hoạt động.
- Chỉ trả ID, họ tên, email, ngành trong khoa, học kỳ và trạng thái tài khoản sinh viên. Không trả thông tin xác thực.
- Báo cáo chỉ gồm sinh viên thuộc khoa và những lớp đang tham gia có môn thuộc khoa đó. Một sinh viên học thêm môn ngoài khoa không làm cán bộ được xem dữ liệu lớp ngoài khoa.
- Hiển thị số bài tập đã công bố, số bài đã nộp/đã chấm và trung bình điểm bài tập đã chấm. Đây chưa phải điểm tổng kết môn hay GPA.
- Không trả nội dung bài nộp, tệp, nhận xét chấm bài hoặc học phí. API báo cáo chỉ đọc; không gọi luồng tạo hóa đơn của dashboard sinh viên.
- Student/Teacher không gọi được API cán bộ. Cán bộ không được xem danh sách tài khoản toàn hệ thống, tự phân quyền, quản lý bài học, chấm điểm, thanh toán hoặc tạo lớp.
- Sinh viên ngoài phạm vi và ID không tồn tại cùng trả 404. Chưa có khoa/khoa đã lưu trữ trả 403. Đổi khoa và thu hồi quyền áp dụng ở request tiếp theo; tải lại trang để cập nhật dữ liệu đang hiển thị.

Admin tiếp tục quản trị tài khoản và phân công. Không tự nâng quyền tài khoản giáo viên/sinh viên thành cán bộ; không tự tạo tài khoản cán bộ với mật khẩu mặc định.

## API

| Endpoint | Quyền | Kết quả |
|---|---|---|
| GET /api/staff/students?q=&page=1 | Staff + staff.students.view + khoa hợp lệ | Danh sách trong khoa, 20 người/trang |
| GET /api/staff/students/{id}/progress | Staff + staff.progress.view + khoa hợp lệ | Hồ sơ cơ bản và tổng hợp học tập trong khoa |

## Cấu trúc

- `Services/StaffAcademicService.cs`: các truy vấn đọc theo khoa và tổng hợp bài tập.
- `Controllers/StaffController.cs`: kiểm tra role/quyền/phạm vi trước khi trả dữ liệu.
- `ClientApp/src/StaffPages.tsx`: dashboard, danh sách và báo cáo. Dùng `useRemote`/useState có sẵn; bộ lọc nằm trên URL, không thêm thư viện state management.
- Admin tạo tài khoản và giao khoa bằng các trang Razor quản trị hiện có.

Chạy vẫn bằng `dotnet run`; quyền mới tự đồng bộ vào MongoDB khi khởi động. Có thể chỉ cập nhật quyền bằng `dotnet run -- --sync-permissions`.

Kiểm thử database tạm, backend, Chromium và Postman:

```bash
python tests/staff_integration.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll --postman
```

Kho học liệu dùng chung, duyệt tài liệu và ngân hàng câu hỏi là các mốc tiếp theo; chưa nằm trong thay đổi này.

Kết quả kiểm thử: build .NET/TypeScript thành công, 0 lỗi/0 cảnh báo .NET; 13/13 ca Chromium, 43/43 assertion Postman và 38 nhóm hồi quy đạt. Có kiểm thử Admin bỏ/giao lại khoa qua giao diện trong khi phiên cán bộ vẫn hoạt động.
