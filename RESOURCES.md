# Kho học liệu dùng chung

## Bắt đầu sử dụng

Chạy như trước bằng `dotnet run`. Ứng dụng hiện có 27 quyền hệ thống sau khi bổ sung ngân hàng câu hỏi và bài kiểm tra.

Với tài khoản đã tồn tại, Admin vào **Phân quyền / hồ sơ** và chọn các quyền cần thiết:

| Vai trò | Quyền kho học liệu |
|---|---|
| Giáo viên | Xem kho học liệu + Soạn và công bố học liệu |
| Sinh viên | Xem kho học liệu |
| Cán bộ | Xem kho học liệu + Duyệt học liệu trong khoa; cần được giao khoa |
| Admin | Có quyền quản trị kho qua vai trò, không cần tick thêm |

Không tự cấp quyền mới cho tài khoản đã được Admin cấu hình. Tài khoản tạo mới có bộ quyền mặc định theo vai trò. Nếu menu **Kho học liệu** chưa xuất hiện, kiểm tra quyền rồi tải lại trang/phiên đăng nhập.

1. Giáo viên vào React → **Kho học liệu → Thêm học liệu**.
2. Nhập tên, loại, tác giả, nguồn, giấy phép/điều kiện sử dụng; chọn một hoặc nhiều môn đang được phân công. Thêm tệp hoặc đường dẫn http/https và lưu nháp.
3. Chọn **Gửi duyệt**. Admin hoặc cán bộ được giao khoa đọc tài liệu, kiểm tra nguồn/giấy phép và chọn **Phê duyệt** hoặc **Yêu cầu chỉnh sửa** kèm nhận xét.
4. Tác giả mở lại tài liệu và chọn **Công bố** sau khi được duyệt.
5. Sinh viên xem/tải bản đã công bố theo quyền truy cập. Bản nháp/chờ duyệt không xuất hiện với người học.

Tác giả và người sửa phiên bản không được tự duyệt, kể cả Admin. Vì vậy luồng đơn giản nhất là giáo viên soạn → Admin duyệt → giáo viên công bố. Tài liệu do Admin soạn cần một cán bộ đúng phạm vi hoặc Admin khác duyệt.

## Quy trình và phiên bản

| Trạng thái | Thao tác tiếp theo |
|---|---|
| Nháp | Gửi duyệt hoặc tạo bản chỉnh sửa mới |
| Chờ duyệt | Người có thẩm quyền phê duyệt/từ chối; tác giả không sửa âm thầm |
| Đã duyệt | Tác giả công bố hoặc tạo bản nháp mới |
| Cần chỉnh sửa | Xem nhận xét, tạo phiên bản mới rồi gửi duyệt |
| Đã công bố | Tiếp tục phục vụ người học; có thể tạo bản nháp mới |
| Đã lưu trữ | Ẩn với người học và chặn tải tệp; chủ tài liệu/Admin vẫn đối chiếu được lịch sử |

Mỗi lần lưu chỉnh sửa tạo phiên bản mới, giữ nguyên nội dung, nguồn, môn, quyền truy cập, tệp và kết quả duyệt của các bản trước. Bản công bố cũ tiếp tục được phục vụ trong khi bản mới đang soạn/duyệt. Khi bản mới được công bố, người học chuyển sang bản đó và không tải được bản cũ bằng cách đổi URL.

Mỗi tài liệu tối đa 50 phiên bản để giới hạn kích thước document của dự án cá nhân. Các thao tác dùng revision để chặn ghi đè/duyệt từ tab cũ; khi có xung đột hãy tải lại trang. Phiên bản, kết quả duyệt và con trỏ bản công bố nằm cùng document MongoDB và được cập nhật có điều kiện trong một thao tác.

## Dùng chung và tệp

- Một bản học liệu liên kết tối đa 20 môn. Một tệp vật lý được dùng chung cho các môn đó và có thể giữ lại qua nhiều phiên bản.
- Tối đa 5 tệp/bản, 25 MB/tệp; dùng cùng danh sách định dạng tài liệu cho phép của ứng dụng.
- SHA-256 giúp tái sử dụng tệp trùng nội dung trong kho của cùng người tải. Không chia sẻ ID hay kiểm tra tồn tại tệp riêng của người khác. Các upload trùng diễn ra đồng thời vẫn có thể tạo bản sao; đây chưa phải kho blob chống trùng toàn cục.
- Chỉ tải qua URL gắn tài liệu + phiên bản + ID tệp. Có ID tệp không đủ để tải bản nháp, bản cũ hoặc tài liệu ngoài phạm vi.
- Tệp đã gắn với bất kỳ phiên bản nào không bị xóa bởi thao tác hoàn tác upload. Lưu trữ không xóa vật lý lịch sử/tệp.
- Nội dung URL không được backend tự tải về; chỉ nhận đường dẫn http/https và mở khi người dùng chủ động chọn.

## Quyền truy cập khác quyền sử dụng

- **Theo môn học:** sinh viên cần đang tham gia lớp có môn liên kết; giáo viên cần được phân công môn; cán bộ xem bản công bố thuộc khoa được giao.
- **Trong trường:** các tài khoản đã đăng nhập và có `resources.view` được đọc bản công bố, kể cả chưa tham gia môn. Không công khai tài liệu cho khách chưa đăng nhập.
- Tác giả được quản lý tài liệu của mình trong các môn được phân công. Admin có thể quản trị tài liệu; người học không được sửa hoặc công bố.
- Cán bộ chỉ duyệt tài liệu khi toàn bộ môn liên kết thuộc khoa đang được giao và còn hoạt động. Tài liệu liên kết nhiều khoa cần Admin không phải người soạn duyệt.
- Giấy phép và điều kiện sử dụng là thông tin riêng, không được suy ra từ quyền tải. Người duyệt phải kiểm tra nội dung này; hệ thống không tự xác minh bản quyền.
- Quyền xem bài học, quyền xem kho và quyền xem báo cáo cán bộ là các quyền độc lập. Cấp quyền kho không cấp quyền chấm điểm hay xem dữ liệu sinh viên ngoài khoa.

## Cấu trúc và giới hạn

- `Models/LearningResource.cs`: document phiên bản và DTO.
- `Services/ResourceService.cs`: kiểm tra chủ sở hữu/môn/khoa, quyền đọc, phiên bản và chuyển trạng thái.
- `Controllers/ResourceController.cs`: API, CSRF, upload và tải tệp có kiểm tra phiên bản.
- `ClientApp/src/ResourcePages.tsx`: danh sách, biểu mẫu, lịch sử và duyệt. Dùng useState/useRemote/useAction hiện có; không thêm thư viện quản lý state.
- Collection `Resources` độc lập với bài học/bài tập; không chuyển hoặc xóa tệp cũ của các phân hệ đó.

Danh sách hiện đọc catalog rồi lọc quyền và phân trang ở server, phù hợp dữ liệu nhỏ của bài tập cá nhân. Chưa có tìm kiếm full-text, phân công reviewer cá nhân, khôi phục tài liệu đã lưu trữ hoặc kho blob chống trùng giữa nhiều chủ sở hữu. Ngân hàng câu hỏi/AI là mốc sau.

## Kiểm thử

```bash
python tests/resources_integration.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll --postman
```

Runner dùng database tạm, chạy cả các bộ kiểm thử trước và kiểm tra chia sẻ hai môn, SHA-256, nguồn/URL/tệp không hợp lệ, chặn tự duyệt, kiểm tra khoa, phiên bản cũ/mới, duyệt lỗi thời, chế độ trong trường và lưu trữ.

Kết quả kiểm thử bản này: build .NET/React TypeScript thành công, .NET 0 lỗi/0 cảnh báo; 14/14 ca Chromium, 43/43 assertion Postman và 42 nhóm hồi quy đạt. Bao gồm giả mạo trạng thái/chủ sở hữu, thu hồi quyền tải tệp trong phiên cũ và mất phân công khoa khi duyệt.
