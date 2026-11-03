# LEARNSPACE – TÀI LIỆU HỌC VÀ KỊCH BẢN THUYẾT TRÌNH

Tài liệu này có hai mục đích:

1. Giúp người trình bày hiểu bản chất các công nghệ và thuật ngữ bảo mật.
2. Cung cấp lời nói ngắn gọn cho từng slide, không yêu cầu đọc nguyên văn toàn bộ tài liệu.

## PHẦN I – HIỂU TOÀN BỘ HỆ THỐNG

### 1. LearnSpace thực chất là gì?

LearnSpace là một ứng dụng web quản lý hoạt động học tập. Ứng dụng có bốn nhóm người dùng:

- **Admin:** quản lý toàn hệ thống, tài khoản và quyền.
- **Staff:** cán bộ quản lý trong phạm vi một khoa.
- **Teacher:** giảng viên quản lý môn và lớp được phân công.
- **Student:** sinh viên sử dụng dữ liệu của chính mình và các lớp đã ghi danh.

Điểm quan trọng của đề tài không chỉ là “có trang quản lý lớp” hay “có trang quiz”. Câu hỏi bảo mật thực sự là:

> Khi nhiều vai trò dùng chung một hệ thống, làm sao đảm bảo mỗi người chỉ được dùng đúng chức năng và đúng dữ liệu thuộc phạm vi của mình?

Ví dụ:

- Teacher A có quyền chấm bài nhưng không được chấm lớp của Teacher B.
- Student A có quyền xem bài nộp nhưng chỉ được xem bài của chính mình.
- Staff khoa Kinh tế có quyền xem báo cáo nhưng không được xem dữ liệu khoa khác.
- Người chưa đăng nhập không được gọi API dữ liệu.

### 2. Frontend, backend và database

#### Frontend

Frontend là phần chạy trong trình duyệt và người dùng nhìn thấy. Dự án dùng **React** và **TypeScript**.

React chia giao diện thành các component. Khi state thay đổi, React cập nhật phần giao diện tương ứng. Ví dụ `QuizPage` tải danh sách quiz và hiển thị nút khác nhau cho Teacher và Student.

TypeScript bổ sung kiểu dữ liệu cho JavaScript. Nó giúp phát hiện lỗi như dùng một chuỗi ở nơi cần số hoặc đọc thuộc tính không tồn tại. TypeScript giúp code ổn định hơn nhưng không phải cơ chế bảo mật. Người dùng vẫn có thể bỏ qua giao diện và tự gửi request, nên backend phải kiểm tra lại mọi thứ.

#### Backend

Backend chạy trên máy chủ. Dự án dùng **ASP.NET Core**. Backend đảm nhiệm:

- Nhận HTTP request.
- Chuyển JSON thành model C#.
- Kiểm tra dữ liệu đầu vào.
- Xác định người gửi request.
- Kiểm tra role, permission và phạm vi dữ liệu.
- Thực hiện quy tắc nghiệp vụ.
- Đọc hoặc ghi MongoDB.
- Trả DTO an toàn cho frontend.

#### Database

MongoDB lưu dữ liệu thành các document gần giống JSON. Mỗi document có `_id` kiểu `ObjectId`. Các collection liên kết bằng ObjectId, ví dụ:

```text
Subject.DepartmentId → Department.Id
ClassRoom.SubjectId → Subject.Id
Lesson.ClassId → ClassRoom.Id
Submission.AssignmentId → Assignment.Id
```

MongoDB không tự thực thi foreign key giống SQL Server. Service của ứng dụng phải kiểm tra ID có tồn tại và có thuộc đúng phạm vi hay không.

### 3. React, Context và reducer

React Context dùng để chia sẻ dữ liệu cho nhiều component. Trong dự án, thông tin phiên đăng nhập cần dùng ở thanh menu, dashboard và các trang nghiệp vụ nên được đặt trong `AuthProvider`.

Reducer dùng khi state có nhiều trạng thái liên quan, ví dụ:

```text
loading → authenticated
loading → anonymous
authenticated → anonymous khi logout
```

Không dùng Redux vì state toàn cục của dự án chưa phức tạp tới mức cần một thư viện quản lý cache lớn. Form, filter và trạng thái tải dữ liệu được đặt gần component bằng `useState` hoặc hook dùng chung.

### 4. API, HTTP method và status code

API là giao diện giao tiếp giữa React và backend.

- `GET`: lấy dữ liệu, không nên thay đổi trạng thái.
- `POST`: tạo mới hoặc thực hiện hành động.
- `PUT`: cập nhật.
- `DELETE`: xóa hoặc lưu trữ.

Các status code quan trọng:

- `200 OK`: xử lý thành công.
- `201 Created`: tạo dữ liệu thành công.
- `204 No Content`: thành công nhưng không cần response body.
- `400 Bad Request`: dữ liệu hoặc CSRF token không hợp lệ.
- `401 Unauthorized`: chưa đăng nhập hoặc cookie hết hiệu lực.
- `403 Forbidden`: đã đăng nhập nhưng thiếu quyền.
- `404 Not Found`: tài nguyên không tồn tại hoặc được cố ý ẩn vì ngoài phạm vi.
- `409 Conflict`: dữ liệu đã thay đổi, revision cũ hoặc trạng thái xung đột.
- `429 Too Many Requests`: vượt giới hạn request.
- `500`: lỗi hệ thống; production không được trả stack trace.

### 5. DTO là gì và vì sao quan trọng?

Model database có thể chứa trường nhạy cảm như `PasswordHash`, `FailedLoginAttempts`, `CorrectOption` hoặc `TeacherComment`. Không nên serialize nguyên model và trả cho client.

DTO là kiểu dữ liệu dành riêng cho request hoặc response. Ví dụ `ApiUserResponse` chỉ chứa:

```text
Id, FullName, Email, Role, IsActive, CreatedAt
```

Nó không chứa `PasswordHash` hoặc lịch sử mật khẩu. DTO giảm nguy cơ rò rỉ dữ liệu do lập trình viên vô tình trả thừa trường.

## PHẦN II – KIẾN THỨC BẢO MẬT CỐT LÕI

### 6. Authentication và Authorization khác nhau thế nào?

**Authentication – xác thực:** trả lời câu hỏi “Bạn là ai?”. Ví dụ đăng nhập bằng email và mật khẩu.

**Authorization – phân quyền:** trả lời câu hỏi “Bạn được phép làm gì?”. Ví dụ Teacher được tạo bài tập.

**Data scope – phạm vi dữ liệu:** trả lời câu hỏi “Bạn được làm việc đó trên bản ghi nào?”. Ví dụ Teacher chỉ tạo bài tập cho lớp mình phụ trách.

Một request an toàn phải qua cả ba lớp. Chỉ có `[Authorize]` là chưa đủ vì thuộc tính đó mới xác nhận người dùng đã đăng nhập.

### 7. Role, permission và ownership

Role là nhóm lớn như Admin, Staff, Teacher, Student.

Permission là quyền cụ thể như:

```text
classes.view
lessons.manage
resources.review
quizzes.attempt
reports.view
```

Ownership hoặc scope là quan hệ với dữ liệu cụ thể:

- `ClassRoom.TeacherId == currentUser.Id`
- `ClassRoom.StudentIds` chứa `currentUser.Id`
- `Subject.DepartmentId == currentUser.DepartmentId`
- `Submission.StudentId == currentUser.Id`

Role và permission ngăn gọi sai chức năng. Ownership ngăn dùng đúng chức năng trên dữ liệu của người khác.

### 8. Cookie Authentication

Sau khi đăng nhập thành công, ASP.NET Core tạo authentication cookie đã ký và mã hóa. Trình duyệt gửi cookie trong các request tiếp theo.

Các thuộc tính quan trọng:

- **HttpOnly:** JavaScript không đọc cookie được.
- **Secure:** production chỉ gửi cookie qua HTTPS.
- **SameSite:** hạn chế gửi cookie trong request xuất phát từ website khác.
- **Expiration:** cookie có thời hạn.

Cookie không chứa mật khẩu. Nó chứa claim như user ID, role, permission và password version.

Mỗi request, `AppCookieAuthenticationEvents.ValidatePrincipal` đọc lại User trong MongoDB. Nếu tài khoản bị khóa, role thay đổi hoặc password version không khớp, cookie bị từ chối.

### 9. Vì sao dự án không bắt buộc dùng JWT?

JWT phù hợp khi nhiều dịch vụ hoặc mobile app cần bearer token độc lập. LearnSpace là React và ASP.NET Core chạy cùng origin, nên cookie HttpOnly đơn giản và hạn chế việc JavaScript phải giữ token.

JWT không tự động an toàn hơn. Nếu lưu JWT trong localStorage thì XSS có thể đọc token. JWT cũng cần thiết kế refresh, thu hồi và hết hạn. Với dự án này, cookie được đối chiếu database mỗi request giúp khóa tài khoản hoặc đổi quyền có hiệu lực ngay.

### 10. Hash mật khẩu và BCrypt

Mật khẩu không được mã hóa theo kiểu có thể giải mã ngược. Nó được **hash**.

BCrypt:

- Tự tạo salt khác nhau cho từng hash.
- Cố ý chạy chậm để làm brute force tốn thời gian.
- Lưu cost trong chuỗi hash để có thể điều chỉnh.

Khi đăng nhập, hệ thống chạy `BCrypt.Verify(password, storedHash)`. Nó không giải mã hash.

Dự án lưu tối đa năm hash cũ trong `PasswordHistoryHashes` để không cho người dùng quay lại mật khẩu gần đây.

### 11. Lockout và update nguyên tử

Nếu nhiều request đăng nhập sai đến cùng lúc, việc đọc bộ đếm rồi ghi lại có thể gây mất cập nhật. Dự án dùng update có điều kiện theo giá trị cũ. Chỉ request khớp trạng thái hiện tại được ghi; request khác phải đọc lại và thử lại.

Khi số lần sai đạt ngưỡng, `LockoutUntil` được đặt trong tương lai. Trong thời gian đó, cả mật khẩu đúng cũng bị từ chối.

### 12. OTP quên mật khẩu

OTP gồm sáu số, có thời hạn 10 phút và chỉ dùng một lần. Database không cần lưu OTP rõ; có thể lưu hash OTP.

Luồng an toàn:

1. Người dùng nhập email.
2. Server tạo OTP tạm ở trạng thái chờ gửi.
3. SMTP kết nối TLS và gửi email.
4. Chỉ khi SMTP chấp nhận email, OTP mới được kích hoạt.
5. Nhập đúng OTP và mật khẩu mới.
6. Token bị đánh dấu đã dùng và cookie cũ bị vô hiệu.

Nếu gửi email thất bại, hệ thống không cho chuyển sang màn hình nhập một OTP chưa được gửi.

### 13. IDOR là gì?

IDOR – Insecure Direct Object Reference – xảy ra khi ứng dụng tin ID do client gửi.

Ví dụ URL:

```text
/api/submissions/ID_CUA_SINH_VIEN_B
```

Nếu server chỉ tìm theo ID thì Student A có thể xem bài Student B. Cách đúng là tìm theo ID kết hợp danh tính:

```text
Submission.Id == requestedId
AND Submission.StudentId == currentUser.Id
```

Với Teacher, service đi từ Submission → Assignment → ClassRoom rồi kiểm tra `TeacherId`.

### 14. CSRF là gì?

CSRF lợi dụng việc trình duyệt tự gửi cookie.

Ví dụ người dùng đang đăng nhập LearnSpace rồi mở một trang độc hại. Trang đó gửi POST tới LearnSpace. Cookie có thể được trình duyệt đính kèm, nên server cần một bằng chứng bổ sung mà trang độc hại không có: antiforgery token.

React lấy token từ backend và gửi trong header `RequestVerificationToken`. Controller dùng `[ValidateAntiForgeryToken]`. Thiếu hoặc sai token trả 400 và không ghi dữ liệu.

### 15. XSS là gì?

XSS xảy ra khi nội dung người dùng nhập được coi là HTML hoặc JavaScript.

Ví dụ:

```html
<script>fetch('/api/users').then(...)</script>
```

React mặc định escape chuỗi khi dùng:

```tsx
<p>{message}</p>
```

Không nên dùng `dangerouslySetInnerHTML` với dữ liệu người dùng trừ khi đã làm sạch bằng thư viện chuyên dụng.

### 16. NoSQL Injection

Trong MongoDB, payload nguy hiểm có thể giống:

```json
{ "email": { "$ne": null } }
```

Nếu backend nhận raw BSON và ghép trực tiếp vào filter, điều kiện có thể thay đổi. LearnSpace nhận `email` dưới dạng `string` có validation và dùng MongoDB Driver typed filter:

```csharp
Users.Find(x => x.Email == normalizedEmail)
```

Payload object không bind được vào string nên bị từ chối.

### 17. Bảo mật file upload

Không thể tin:

- Tên file do client gửi.
- Phần mở rộng.
- MIME type.

Kẻ tấn công có thể đổi `malware.exe` thành `tailieu.pdf`. Vì vậy backend kiểm tra magic bytes, tức chữ ký nhị phân ở đầu file. File được lưu bằng ObjectId trong GridFS và không được phục vụ như static file.

Download phải kiểm tra:

- Người tải là owner, thành viên lớp hoặc người có quyền.
- File có gắn đúng purpose/tài nguyên.
- Tài nguyên chưa bị archive hoặc vẫn được phép xem.

### 18. Revision và kiểm soát cập nhật đồng thời

Nếu hai người cùng mở bản ghi revision 3, người thứ nhất lưu thành revision 4. Request của người thứ hai vẫn gửi revision 3 thì phải bị từ chối 409. Nếu không, thay đổi mới có thể bị ghi đè âm thầm.

Revision được dùng trong học liệu, câu hỏi, quiz và ticket.

### 19. Tại sao phải tách tác giả và người duyệt?

Đây là nguyên tắc separation of duties. Người tạo nội dung không tự xác nhận nội dung của mình là hợp lệ.

Học liệu và câu hỏi đi qua:

```text
Draft → PendingReview → Approved/Rejected → Published
```

Reviewer phải có permission, đúng phạm vi khoa và khác tác giả. Lịch sử version/review được giữ để truy vết.

### 20. Quiz snapshot

Khi Student bắt đầu quiz, hệ thống sao chép nội dung câu hỏi, lựa chọn, đáp án và giải thích vào `QuizAttempt.Questions`.

Snapshot cần thiết vì câu hỏi gốc có thể được Teacher sửa hoặc archive. Bài đã làm phải giữ đúng nội dung tại thời điểm bắt đầu để điểm và lịch sử không thay đổi.

Trước khi nộp, response chỉ trả câu hỏi và options. `CorrectOption` chỉ được trả sau khi bài đã submit.

### 21. Audit log và LearningEvent

`ActivityLog` dành cho sự kiện xác thực và bảo mật:

- Email/user ID.
- Hành động.
- Thành công hay thất bại.
- IP, user agent và thời gian.

`LearningEvent` dành cho nghiệp vụ học tập:

- Ai thực hiện.
- Vai trò.
- Khoa/lớp/sinh viên liên quan.
- Loại entity và ID.
- Nội dung tóm tắt an toàn.

Không ghi password, OTP, connection string, API key hoặc đáp án chi tiết không cần thiết.

### 22. Rate limiting

Rate limiting giới hạn số request trong một cửa sổ thời gian. Dự án áp dụng cho login, password reset và AI.

Nó giúp giảm brute force và lạm dụng tài nguyên, nhưng không thay thế lockout. Rate limit thường theo IP; lockout theo tài khoản. Hai cơ chế bảo vệ hai góc khác nhau.

### 23. Health check

- `/health/live`: tiến trình ứng dụng còn hoạt động.
- `/health/ready`: các phụ thuộc quan trọng như MongoDB đã sẵn sàng.

Hệ thống giám sát hoặc Render có thể gọi endpoint này để phát hiện lỗi.

### 24. Backup và restore

Backup chỉ có giá trị khi restore thành công. Quy trình an toàn là restore sang database thử, kiểm tra số bản ghi và nghiệp vụ rồi mới coi backup sử dụng được.

Dự án đã thử `mongodump` và `mongorestore` trên database local tạm, khôi phục đúng 3/3 document. Atlas snapshot/PITR là cơ chế vận hành riêng trên cloud.

### 25. Dependency scan

Ứng dụng phụ thuộc thư viện bên ngoài. Lỗ hổng có thể tồn tại trong dependency dù code của mình đúng.

- `npm audit --omit=dev` kiểm tra dependency frontend production.
- `dotnet list package --vulnerable --include-transitive` kiểm tra NuGet trực tiếp và gián tiếp.

Kết quả quét chỉ đúng tại thời điểm chạy; cần quét lại định kỳ.

### 26. Secret và biến môi trường

Các giá trị sau không được commit:

- MongoDB connection string.
- Gmail App Password.
- OpenAI API key.
- VNPAY Hash Secret.

Chúng được cấu hình bằng `.env` local bị Git ignore hoặc Environment Variables trên Render.

## PHẦN III – KỊCH BẢN NÓI THEO 24 SLIDE

Phần này chỉ là lời nói gợi ý. Không cần đọc nguyên văn; hãy hiểu ý và nói bằng cách của mình.

### Slide 1 – Giới thiệu

“Em là Nguyễn Hải Phong. Đề tài LearnSpace là hệ thống quản lý học tập và học liệu. Trọng tâm của em là cùng một hệ thống có nhiều vai trò nhưng mỗi người chỉ truy cập đúng chức năng và dữ liệu của mình. Em sẽ trình bày kiến trúc, code bảo mật và demo hành vi thực tế.”

### Slide 2 – Bài toán

“Hệ thống cần quản lý học tập, nội dung và dữ liệu cá nhân. Nếu chỉ làm CRUD thì Teacher hoặc Student có thể gọi nhầm API và thấy dữ liệu người khác. Câu hỏi chính là làm sao dễ sử dụng nhưng không tin quyền và ID do trình duyệt tự khai báo.”

### Slide 3 – Cách giải quyết

“Mỗi request đi qua năm bước: đăng nhập, kiểm tra quyền, kiểm tra phạm vi, xử lý dữ liệu và trả DTO. Nếu sai ở bước nào, request dừng ở bước đó.”

### Slide 4 – Vai trò

“Admin quản trị toàn hệ thống. Staff bị giới hạn theo khoa. Teacher bị giới hạn theo lớp và môn. Student chỉ dùng dữ liệu chính chủ. Role mới là lớp đầu; service vẫn phải kiểm tra bản ghi cụ thể.”

### Slide 5 – Kiến trúc

“React không kết nối MongoDB trực tiếp. Request vào middleware, controller và service rồi mới tới MongoDB hoặc GridFS. Response được map sang DTO. Sự kiện quan trọng được ghi audit.”

### Slide 6 – Công nghệ

“React tạo giao diện; TypeScript kiểm tra kiểu. ASP.NET Core xử lý API và quyền. MongoDB Atlas lưu dữ liệu. GridFS lưu file. BCrypt băm mật khẩu. Docker giúp triển khai cùng một cấu hình.”

### Slide 7 – Dữ liệu

“Các collection liên kết bằng ObjectId. MongoDB không có foreign key vật lý nên service kiểm tra liên kết. Một số dữ liệu như message, history và version được nhúng trong document cha.”

### Slide 8 – Ba lớp kiểm tra

“Authentication kiểm tra người dùng là ai. Authorization kiểm tra được dùng chức năng nào. Data scope kiểm tra được thao tác trên bản ghi nào. Cần đủ cả ba.”

### Slide 9 – Đăng nhập

“Server chuẩn hóa email, kiểm tra tài khoản và thời gian khóa, rồi dùng BCrypt Verify. Sai nhiều lần tăng bộ đếm bằng update nguyên tử. Thông báo lỗi không tiết lộ email có tồn tại hay không.”

### Slide 10 – Cookie

“Cookie được đặt HttpOnly, Secure và SameSite. Mỗi request server đọc lại User. Nếu tài khoản bị khóa, role thay đổi hoặc password version khác thì cookie bị hủy.”

### Slide 11 – Mật khẩu và OTP

“Mật khẩu lưu bằng BCrypt, có lịch sử năm hash. OTP sáu số, 10 phút, dùng một lần và có giới hạn gửi. Reset thành công hủy phiên cũ.”

### Slide 12 – IDOR

“Có quyền xem lớp chưa đủ. TeacherId phải đúng giảng viên, StudentIds phải chứa sinh viên, DepartmentId phải đúng khoa. Đổi ID trên URL không mở rộng quyền.”

### Slide 13 – Ba rủi ro web

“CSRF lợi dụng cookie, XSS lợi dụng nội dung hiển thị, NoSQL Injection lợi dụng truy vấn. Mỗi loại được chặn bằng token, output encoding và typed filter.”

### Slide 14 – CSRF

“Các request thay đổi dữ liệu phải có antiforgery token do ứng dụng cấp. Trang ngoài có thể khiến trình duyệt gửi cookie nhưng không có token hợp lệ.”

### Slide 15 – XSS và NoSQL Injection

“React hiển thị chuỗi ở dạng text nên script không chạy. Backend nhận email dạng string và dùng biểu thức C# để tạo filter MongoDB, không nhận raw BSON từ client.”

### Slide 16 – File

“Server kiểm tra dung lượng, đuôi, MIME và magic bytes. File nằm trong GridFS ngoài webroot và tải qua API có kiểm tra quyền.”

### Slide 17 – Kiểm duyệt

“Nội dung đi từ Draft đến review và publish. Tác giả không tự duyệt. Revision ngăn ghi đè thay đổi của người khác và version giữ lịch sử.”

### Slide 18 – Quiz

“Quiz chỉ dùng câu hỏi published. Student phải thuộc lớp. API không trả đáp án trước khi nộp. Attempt lưu snapshot và server chấm một lần.”

### Slide 19 – Nhật ký

“ActivityLog dùng cho đăng nhập và bảo mật. LearningEvent dùng cho hoạt động học tập. Response lỗi production không trả stack trace; request ID giúp tìm log.”

### Slide 20 – Vận hành

“Rate limit giảm lạm dụng. Health check phát hiện dịch vụ lỗi. Index và pagination giảm tải. Backup phải được restore thử. Secret nằm ngoài source và dependency được quét.”

### Slide 21 – AI

“AI chỉ tạo Draft theo JSON Schema. API key ở backend. Teacher vẫn phải sửa và duyệt. AI lỗi thì không tạo bản ghi dở dang.”

### Slide 22 – Kiểm thử

“Test không chỉ nhìn status code mà còn kiểm tra database có thay đổi sai hay không. Bộ hiện tại có 64 nhóm regression, 20 test React và smoke request đồng thời.”

### Slide 23 – Demo

“Em demo theo vai trò: Admin phân quyền, Staff xem phạm vi khoa, Teacher quản lý lớp và Student sử dụng nội dung. Mỗi bước có một thao tác hợp lệ và một thao tác ngoài phạm vi.”

### Slide 24 – Kết luận

“Bảo mật nằm trong luồng nghiệp vụ. Mỗi request phải đúng người, đúng chức năng, đúng dữ liệu và có thể truy vết. Frontend hỗ trợ trải nghiệm; backend mới là nơi quyết định quyền.”

## PHẦN IV – KỊCH BẢN DEMO CHI TIẾT

### Demo Admin

1. Đăng nhập Admin.
2. Mở trang phân quyền.
3. Chọn Staff hoặc Teacher và chỉ ra role, permission, department/subject scope.
4. Giải thích thay đổi quyền có hiệu lực với cookie hiện tại vì server đọc lại User mỗi request.

### Demo Staff

1. Đăng nhập Staff.
2. Mở sinh viên trong khoa hoặc báo cáo.
3. Chỉ ra danh sách chỉ thuộc khoa được gán.
4. Giải thích client không truyền department để mở rộng phạm vi; server dùng DepartmentId trong User.

### Demo Teacher

1. Đăng nhập Teacher.
2. Mở lớp được phân công.
3. Mở bài học, học liệu hoặc quiz.
4. Chỉ trạng thái Draft/Published và quy trình review.
5. Giải thích Teacher khác gọi API sẽ bị 403 vì TeacherId không khớp.

### Demo Student

1. Đăng nhập Student.
2. Mở lớp, quiz hoặc báo cáo.
3. Chỉ rằng đáp án đúng không xuất hiện trước khi submit.
4. Mở hỗ trợ người học và chỉ ticket chính chủ.

### Demo Audit

1. Quay lại Admin.
2. Mở ActivityLog.
3. Chỉ email/user, hành động, thành công/thất bại, IP và thời gian.
4. Nhấn mạnh log không chứa mật khẩu hoặc OTP.

## PHẦN V – CÂU HỎI GIẢNG VIÊN CÓ THỂ HỎI

### Vì sao kiểm tra quyền ở cả controller và service?

Controller kiểm tra quyền dùng chức năng. Service kiểm tra quyền trên dữ liệu cụ thể. Nếu chỉ kiểm tra controller, Teacher có thể dùng chức năng hợp lệ trên lớp của Teacher khác.

### TypeScript có bảo mật không?

TypeScript giảm lỗi khi phát triển nhưng chạy thành JavaScript trong trình duyệt. Người dùng có thể tự gửi request nên backend vẫn phải validation và authorization.

### MongoDB có an toàn hơn SQL Server không?

Không thể kết luận chung. Mức an toàn phụ thuộc cấu hình, quyền database, TLS, backup, audit và code ứng dụng. Dự án dùng MongoDB vì mô hình document và GridFS phù hợp.

### Vì sao không trả 404 cho mọi lỗi quyền?

403 phù hợp khi người dùng biết endpoint nhưng thiếu quyền. 404 có thể dùng khi muốn che sự tồn tại của tài nguyên ngoài phạm vi, ví dụ ticket của Student khác. Cần thống nhất theo loại endpoint.

### Hash và encryption khác nhau thế nào?

Encryption có khóa và có thể giải mã. Hash là một chiều. Mật khẩu dùng hash vì hệ thống không cần khôi phục mật khẩu gốc.

### TLS có bảo vệ dữ liệu trong database không?

TLS bảo vệ dữ liệu khi truyền. Encryption at-rest bảo vệ dữ liệu trên ổ đĩa. Phân quyền bảo vệ ai được truy cập. Cần kết hợp các lớp này.

### Nếu cookie bị đánh cắp thì sao?

HttpOnly, Secure, SameSite và HTTPS giảm nguy cơ. Cookie có thời hạn và được đối chiếu database. Đổi mật khẩu, đổi role hoặc khóa tài khoản làm cookie cũ mất hiệu lực.

### AI có thể đưa đáp án sai không?

Có. Vì vậy AI chỉ tạo Draft và bắt buộc người có chuyên môn kiểm duyệt trước khi publish.

### Backup local đã đủ cho production chưa?

Chưa. Nó chứng minh quy trình dump/restore. Production cần lịch backup, retention, Atlas snapshot/PITR và diễn tập restore định kỳ.
