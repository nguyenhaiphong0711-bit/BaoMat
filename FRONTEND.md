# React + TypeScript trong cùng ứng dụng ASP.NET

## Chạy trên máy phát triển

Cần .NET 10 SDK, Node.js 24 LTS (tối thiểu 22.12), MongoDB đang chạy và cấu hình `.env` hiện có. Từ thư mục chứa `LMS.csproj`:

```bash
dotnet run
```

Lần đầu, MSBuild tự `npm ci` theo lockfile và build TypeScript/React. Nếu dependency không thay đổi thì những lần sau không cài lại. ASP.NET Development tự khởi động Vite, chờ Vite sẵn sàng rồi phục vụ React tại `http://localhost:5000/app/`; mở địa chỉ gốc cũng chuyển tới giao diện đó. Không cần mở terminal thứ hai hoặc viết server Node.js. Ctrl+C dừng ứng dụng và Vite.

Mặc định dùng profile `Properties/launchSettings.json`. Nếu cổng 5173 đã được dùng, có thể đặt biến `Frontend__DevServerPort` thành cổng trống; cổng ASP.NET được cấu hình riêng bằng profile/`ASPNETCORE_URLS`. Không khởi động thêm Vite thủ công trên cùng cổng khi đang dùng `dotnet run`.

Lỗi `npm` không tồn tại: cài Node.js rồi mở lại VS Code/Terminal để cập nhật PATH. MongoDB không chạy hoặc `.env` sai vẫn phải được xử lý như trước; React không thay đổi các yêu cầu này. Lần đầu cài thư viện cần Internet tới npm/NuGet.

## Phạm vi giao diện ở bản đầu

Đã dùng React cho:

- Đăng nhập, yêu cầu OTP và đặt lại mật khẩu.
- Dashboard theo tài khoản, số lớp thực và danh mục môn học thực.
- Danh mục môn học: tìm kiếm, lọc khoa, phân trang; Admin thêm/sửa/xóa qua API.
- Danh sách lớp theo phạm vi của người dùng, thông tin lớp, bài học và bài tập.

Các màn hình tạo/sửa lớp, quản lý bài học/bài tập, nộp bài, chấm bài, lịch, học vụ, học phí, người dùng và phân quyền vẫn dùng Razor hiện có. React dẫn tới đúng trang đó với cùng phiên đăng nhập. Thanh bên Razor có liên kết “Không gian React” để quay lại. Đây là chuyển đổi từng phần, chưa phải thay thế toàn bộ Razor.

## Cấu trúc để học và mở rộng

```text
ClientApp/
  src/auth.tsx       Context + useReducer cho phiên đăng nhập
  src/api.ts         fetch, lỗi API và CSRF
  src/hooks.ts       useEffect/useState cho dữ liệu riêng của trang
  src/App.tsx        Router, bố cục, menu theo quyền
  src/AuthPages.tsx  Đăng nhập và khôi phục mật khẩu
  src/Pages.tsx      Dashboard, môn học, lớp và nội dung lớp
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
