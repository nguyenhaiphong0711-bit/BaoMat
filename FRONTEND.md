# React + TypeScript trong cùng ứng dụng ASP.NET

## Chạy trên máy phát triển

Cần .NET 10 SDK, Node.js 24 LTS (tối thiểu 22.12), MongoDB đang chạy và cấu hình `.env` hiện có. Từ thư mục chứa `LMS.csproj`:

```bash
dotnet run
```

Lần đầu, MSBuild tự `npm ci` theo lockfile và build TypeScript/React. Nếu dependency không thay đổi thì những lần sau không cài lại. ASP.NET Development tự khởi động Vite, chờ Vite sẵn sàng rồi phục vụ React tại `http://localhost:5000/app/`; mở địa chỉ gốc cũng chuyển tới giao diện đó. Không cần mở terminal thứ hai hoặc viết server Node.js. Ctrl+C dừng ứng dụng và Vite.

Mặc định dùng profile `Properties/launchSettings.json`. Nếu cổng 5173 đã được dùng, có thể đặt biến `Frontend__DevServerPort` thành cổng trống; cổng ASP.NET được cấu hình riêng bằng profile/`ASPNETCORE_URLS`. Không khởi động thêm Vite thủ công trên cùng cổng khi đang dùng `dotnet run`.

Lỗi `npm` không tồn tại: cài Node.js rồi mở lại VS Code/Terminal để cập nhật PATH. MongoDB không chạy hoặc `.env` sai vẫn phải được xử lý như trước; React không thay đổi các yêu cầu này. Lần đầu cài thư viện cần Internet tới npm/NuGet.

## Phạm vi giao diện hiện tại

Đã dùng React cho:

- Đăng nhập, yêu cầu OTP và đặt lại mật khẩu.
- Dashboard theo tài khoản, số lớp thực và danh mục môn học thực.
- Kho học liệu dùng chung: soạn nháp, liên kết nhiều môn, tệp/đường dẫn, duyệt, công bố, lịch sử và lưu trữ; xem `RESOURCES.md`.
- Cán bộ quản lý: dashboard, tìm kiếm/phân trang sinh viên trong khoa và báo cáo học tập chỉ đọc; xem `STAFF.md`.
- Danh mục môn học: tìm kiếm, lọc khoa, phân trang; Admin thêm/sửa/xóa qua API.
- Danh sách lớp theo phạm vi của người dùng và thông tin lớp.
- Bài học: tạo, sửa, lưu nháp, công bố, lưu trữ và đính kèm tệp bằng React. Giáo viên có thể giữ/bỏ tệp khi sửa; tối đa 5 tệp, 25 MB/tệp. Sinh viên chỉ tải được tệp của bài đã công bố trong lớp của mình.
- Bài tập: tạo/sửa, hạn nộp theo giờ địa phương, tệp đính kèm, công bố và lưu trữ.
- Sinh viên: nộp nội dung hoặc tệp, nộp lại, xem điểm và nhận xét.
- Giáo viên: xem bài nộp, tải tệp, chấm điểm và nhận xét trong lớp được phân công.

Toàn bộ điều hướng nghiệp vụ dùng React: lớp, lịch, đăng ký học, tiến độ, học phí, người dùng, phân quyền, khoa, ngành, học kỳ, duyệt đăng ký và nhật ký. Các URL Razor cũ được chuyển hướng sang route React tương ứng để bookmark cũ tiếp tục hoạt động. View Razor còn trong source chỉ phục vụ tương thích kiểm thử/POST cũ và trang lỗi máy chủ; người dùng không còn được điều hướng tới chúng từ ứng dụng.

Các màn hình quản trị React hỗ trợ tạo và sửa lớp/roster/phân công, tạo và sửa khoa, cấu hình chương trình ngành theo môn-học kỳ-tín chỉ, tạo/hủy buổi học, duyệt giờ dạy và chỉnh hồ sơ học vụ cùng quyền chức năng theo vai trò. Playwright kiểm tra sidebar quản trị không còn liên kết tới URL MVC cũ.

## Cấu trúc để học và mở rộng

```text
ClientApp/
  src/auth.tsx       Context + useReducer cho phiên đăng nhập
  src/api.ts         fetch, lỗi API và CSRF
  src/hooks.ts       useEffect/useState cho dữ liệu riêng của trang
  src/App.tsx        Router, bố cục, menu theo quyền
  src/AuthPages.tsx  Đăng nhập và khôi phục mật khẩu
  src/Pages.tsx      Dashboard, môn học, lớp và nội dung lớp
  src/LearningPages.tsx  Biểu mẫu bài học/bài tập, nộp bài và chấm điểm
  src/StaffPages.tsx  Dashboard, sinh viên và tiến trình trong khoa của cán bộ
  src/ResourcePages.tsx  Kho học liệu, biểu mẫu, duyệt và phiên bản
  src/types.ts      Kiểu dữ liệu TypeScript
  e2e/              Kiểm thử Chromium bằng Playwright
```

Không có Redux hoặc TanStack Query. AuthContext chỉ giữ danh tính/role/quyền hiện tại, không giữ tất cả danh sách của hệ thống. Bộ lọc và phân trang nằm trên URL. Mật khẩu không được lưu vào Context/localStorage.

## Bảo mật và phiên đăng nhập

React/API/Razor cùng origin. Cookie xác thực vẫn HttpOnly và không được đọc bởi JavaScript. Các endpoint frontend nằm ở `/api/frontend/*`; API dữ liệu hiện có vẫn giữ ở `/api/*`.

Trước mỗi mutation, `api.ts` lấy request token từ `/api/frontend/csrf` và gửi header `RequestVerificationToken`; token luôn được lấy theo danh tính hiện tại sau đăng nhập/đăng xuất. Login/logout/OTP frontend bắt buộc antiforgery. Các API cũ cũng xác thực antiforgery khi request đến từ browser (Origin hoặc Sec-Fetch-Site). Postman và client server không có các header browser tiếp tục dùng quy trình cookie hiện có.

Menu/nút theo role chỉ là UX. Backend vẫn kiểm tra role, permission và phạm vi tài nguyên trên mỗi request. Không đặt connection string, App Password hoặc API key trong React hay biến `VITE_*`; đây là cấu hình có thể đi vào bundle công khai.

## Build và triển khai

```bash
dotnet publish -c Release -o ./publish
```

Bước publish tự build React và chép assets vào `publish/wwwroot/app`. Trên máy chạy bản publish:

```bash
dotnet LMS.dll
```

Máy runtime không cần Node.js/npm; chỉ máy build cần. Dùng HTTPS cho Production vì cookie xác thực chỉ được gửi qua HTTPS. Giữ các cấu hình MongoDB/email/Bootstrap của backend như README.

Dockerfile có ba stage: Node build React, .NET SDK publish backend, .NET ASP.NET runtime phục vụ cả hai. `SkipFrontendBuild=true` chỉ dành cho Docker khi stage Node đã tạo `ClientApp/dist`; không dùng để bỏ qua lỗi build frontend.

Khi chỉ chạy API hoặc test static frontend trong Development, đặt `Frontend__UseDevServer=false`. Khi đó backend phục vụ React đã build, không khởi động Vite.

## Kiểm thử

TypeScript và bundle:

```bash
npm --prefix ClientApp run build
```

Kiểm thử trình duyệt với ứng dụng Development đang chạy và database thử nghiệm:

```bash
cd ClientApp
npx playwright install chromium
npm run test:e2e
```

Các test CRUD tạo/xóa dữ liệu; không trỏ bộ kiểm thử vào Production. Có thể đặt `E2E_BASE_URL`, `E2E_ADMIN_EMAIL`, `E2E_ADMIN_PASSWORD` cho tài khoản test. Các ca lớp/SMTP chuyên biệt cần dữ liệu của runner tích hợp, nên sẽ skip nếu thiếu cấu hình đó.

Runner tích hợp dùng MongoDB local `baomat-mongo`, SMTP local, database UUID riêng, toàn bộ security regression và Chromium. Sau khi build/publish, đặt `E2E_PUBLISH_DIR` thành thư mục publish rồi chạy:

```bash
python3 tests/react_integration.py --dotnet /path/to/dotnet --dll /path/to/artifacts/bin/LMS/release/LMS.dll --postman
```

Có thể đặt `CHROMIUM_PATH` nếu muốn dùng Chromium đã cài. Runner còn kiểm tra `dotnet run` tự khởi động Vite, Vite dừng cùng host, deep link/asset production và runtime không có Node.js trong PATH. Windows sử dụng nhánh chạy `cmd.exe` để khởi động npm; các lượt kiểm thử tự động ở cloud chạy trên Linux.

## Kết quả xác minh bản tích hợp đầu tiên

- Build/publish .NET + TypeScript + Vite thành công; npm audit không báo advisory tại thời điểm kiểm tra.
- Cài frontend trên bản sao chưa có `node_modules`/`dist`: MSBuild tự cài theo lockfile và build thành công.
- 9 ca Chromium đạt, gồm CRUD môn học, phiên chia sẻ với Razor, CSRF/phân quyền, mobile, lỗi gửi OTP và upload/revert FilePond cũ.
- Runner tổng hợp đạt 33 nhóm hồi quy, có 43/43 assertion Postman; kiểm tra riêng API tóm tắt lớp mới chặn người ngoài lớp và tài khoản chưa đăng nhập.
- `dotnet run` tự khởi động Vite; host dừng thì Vite cũng dừng. Bản publish phục vụ asset/deep link khi PATH không có Node.js.
- Docker build chưa được xác minh hoàn tất trên cloud: tải base image Microsoft bị từ chối HTTP 403 tại `centralus.data.mcr.microsoft.com`. Không tắt TLS hoặc đổi nguồn image để vượt lỗi này. Bản publish trực tiếp đã được chạy và xác minh độc lập.

## State và vòng đời component

Giữ cách tổ chức đơn giản cho dự án cá nhân:

| Loại dữ liệu | Đặt ở đâu | Ví dụ |
|---|---|---|
| Phiên đăng nhập dùng chung | Context + useReducer trong `auth.tsx` | Người dùng, vai trò, quyền |
| Dữ liệu lấy từ backend | `useRemote` tại trang cần dùng | Bài học, bài tập, bài nộp |
| Dữ liệu đang nhập | useState trong biểu mẫu | Tiêu đề, nội dung, tệp chọn, điểm |
| Trạng thái gửi biểu mẫu | `useAction` | Đang gửi, thông báo lỗi |
| Bộ lọc/phân trang | URL search params | Từ khóa, khoa, số trang |

Không sao chép danh sách lớp/bài tập vào AuthContext. Các giá trị tính được như hết hạn hay số tệp được tính từ dữ liệu hiện có, không tạo thêm state. Chỉ dùng reducer cho phiên đăng nhập; biểu mẫu nhỏ dùng useState.

- Mount hoặc URL đổi: `useRemote` tải dữ liệu trong useEffect.
- URL đổi hoặc unmount: cleanup dùng AbortController hủy request cũ; không hiển thị dữ liệu của URL trước.
- Biểu mẫu chỉ mount sau khi dữ liệu tải xong, khởi tạo state một lần. `key` theo ID giúp reset đúng khi đổi đối tượng; không cần effect chép dữ liệu server vào form.
- Nhấn Lưu/Nộp/Chấm: gọi API trong event handler. `useAction` chặn gửi trùng và hủy request khi unmount; không chạy thao tác ghi dữ liệu trong effect.
- Thành công: quay về danh sách hoặc tải lại dữ liệu từ server. Không tự sửa đồng thời nhiều bản sao state.
- Tệp mới chỉ upload khi lưu; nếu lưu thất bại, thử xóa các tệp vừa upload còn chưa được sử dụng. Tệp đã lưu chỉ thay đổi sau khi backend chấp nhận biểu mẫu.

Sinh viên có thể nộp lại trước hạn. Theo nghiệp vụ backend hiện có, nộp lại xóa điểm và nhận xét cũ để giáo viên chấm lại. Giới hạn 5 tệp, 25 MB/tệp và quyền truy cập đều được backend kiểm tra.

Kiểm thử luồng React trên database tạm (cần Docker MongoDB `baomat-mongo`, OpenSSL và Chromium/Playwright):

```bash
python tests/learning_integration.py --dotnet /path/to/dotnet --dll /path/to/LMS.dll
```

Bộ kiểm thử chạy cả kiểm thử bảo mật sẵn có và trình duyệt. Nó kiểm tra nháp/công bố, upload/download, nộp bài chỉ có tệp, chấm điểm, nộp lại, hết hạn và các yêu cầu API giả mạo quyền/tệp.

Kết quả kiểm tra bản bổ sung luồng học tập: build .NET 0 lỗi/0 cảnh báo, build TypeScript/Vite thành công, 11/11 ca Chromium và 29 nhóm hồi quy đạt. Có ca kiểm tra rời trang hủy request đang chờ rồi mở lại tải dữ liệu mới. Các kết quả này áp dụng cho code hiện có, không xác nhận các phân hệ chưa xây dựng.
