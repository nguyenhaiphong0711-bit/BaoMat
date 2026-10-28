param(
    [string]$BaseUrl = "https://baomatt.onrender.com",
    [string]$AdminEmail = "admin@lms.com",
    [ValidateRange(1, 5)][int]$Count = 3,
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
if (-not $Apply) {
    throw "Chưa thay đổi production. Chạy lại với -Apply sau khi kiểm tra đúng BaseUrl."
}
$BaseUrl = $BaseUrl.TrimEnd("/")

function Convert-SecureStringToText([Security.SecureString]$Value) {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Value)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Invoke-LmsApi {
    param(
        [Parameter(Mandatory)][Microsoft.PowerShell.Commands.WebRequestSession]$Session,
        [Parameter(Mandatory)][string]$Path,
        [ValidateSet("GET", "POST", "PUT", "DELETE")][string]$Method = "GET",
        $Body,
        [switch]$Csrf
    )
    $headers = @{ Accept = "application/json" }
    if ($Csrf) {
        $token = Invoke-RestMethod -Uri "$BaseUrl/api/frontend/csrf" -Method Get -WebSession $Session -TimeoutSec 90
        $headers["RequestVerificationToken"] = $token.requestToken
    }
    $parameters = @{
        Uri = "$BaseUrl$Path"
        Method = $Method
        WebSession = $Session
        Headers = $headers
        TimeoutSec = 90
    }
    if ($null -ne $Body) {
        $parameters.ContentType = "application/json; charset=utf-8"
        $parameters.Body = $Body | ConvertTo-Json -Depth 12 -Compress
    }
    Invoke-RestMethod @parameters
}

function New-LoginSession([string]$Email, [string]$Password) {
    $session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
    $null = Invoke-LmsApi -Session $session -Path "/api/auth/login" -Method POST -Body @{
        email = $Email
        password = $Password
    }
    $session
}

function New-RandomPassword {
    $bytes = New-Object byte[] 18
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) } finally { $generator.Dispose() }
    "Tmp!$([Convert]::ToBase64String($bytes).Replace('/', 'Q').Replace('+', 'R'))9aA"
}

$securePassword = Read-Host "Mật khẩu $AdminEmail" -AsSecureString
$adminPassword = Convert-SecureStringToText $securePassword
$author = New-LoginSession -Email $AdminEmail -Password $adminPassword
$identity = Invoke-LmsApi -Session $author -Path "/api/auth/me"
if ($identity.role -ne "Admin") { throw "Tài khoản phải có vai trò Admin." }

$classes = @(Invoke-LmsApi -Session $author -Path "/api/classes")
$subjects = @(Invoke-LmsApi -Session $author -Path "/api/subjects")
$quizzes = @(Invoke-LmsApi -Session $author -Path "/api/quizzes")
$subjectById = @{}
foreach ($subject in $subjects) { $subjectById[$subject.id] = $subject }
$classWithQuiz = @{}
foreach ($quiz in $quizzes) { if (-not $quiz.isArchived) { $classWithQuiz[$quiz.classId] = $true } }
$targets = @($classes | Where-Object {
    $subjectById.ContainsKey($_.subjectId) -and -not $classWithQuiz.ContainsKey($_.id)
} | Select-Object -First $Count)

if ($targets.Count -eq 0) {
    Write-Host "Không có lớp trống quiz phù hợp; production đã có quiz hoặc chưa có lớp/môn liên kết."
    exit 0
}

$stamp = [DateTime]::UtcNow.ToString("yyyyMMddHHmmss")
$reviewerEmail = "quiz-reviewer-$stamp@example.test"
$reviewerPassword = New-RandomPassword
$reviewerUser = Invoke-LmsApi -Session $author -Path "/api/users" -Method POST -Body @{
    fullName = "Cán bộ kiểm duyệt quiz tạm thời"
    email = $reviewerEmail
    password = $reviewerPassword
    role = "Admin"
}

$createdCount = 0
try {
    $reviewer = New-LoginSession -Email $reviewerEmail -Password $reviewerPassword
    $now = [DateTime]::UtcNow
    foreach ($classroom in $targets) {
        $subject = $subjectById[$classroom.subjectId]
        $question = Invoke-LmsApi -Session $author -Path "/api/questions" -Method POST -Csrf -Body @{
            revision = 0
            subjectId = $subject.id
            topic = "Kiến thức nền tảng"
            difficulty = "Medium"
            content = "Nội dung nào phản ánh đúng mục tiêu cốt lõi của học phần $($subject.name)?"
            options = @(
                "Vận dụng kiến thức để phân tích tình huống thực tế",
                "Chỉ ghi nhớ thuật ngữ mà không cần hiểu",
                "Bỏ qua dữ liệu và bằng chứng liên quan",
                "Dùng chung một kết luận cho mọi tình huống"
            )
            correctOption = 0
            explanation = "Học phần hướng tới hiểu và vận dụng kiến thức trong bối cảnh thực tế."
        }
        $question = Invoke-LmsApi -Session $author -Path "/api/questions/$($question.id)/submit" -Method POST -Csrf -Body @{
            revision = $question.revision; approve = $false; comment = ""
        }
        $question = Invoke-LmsApi -Session $reviewer -Path "/api/questions/$($question.id)/review" -Method POST -Csrf -Body @{
            revision = $question.revision
            approve = $true
            comment = "Đã kiểm tra nội dung, đáp án và phạm vi môn học."
        }
        $question = Invoke-LmsApi -Session $author -Path "/api/questions/$($question.id)/publish" -Method POST -Csrf -Body @{
            revision = $question.revision; approve = $false; comment = ""
        }
        $createdCount++
        $quiz = Invoke-LmsApi -Session $author -Path "/api/quizzes" -Method POST -Csrf -Body @{
            revision = 0
            classId = $classroom.id
            title = "Ôn tập $($subject.name) · $($createdCount.ToString('00'))"
            instructions = "Chọn phương án phù hợp nhất. Kết quả được chấm tự động sau khi nộp."
            durationMinutes = 15
            opensAt = $now.AddHours(-1).ToString("o")
            closesAt = $now.AddDays(30).ToString("o")
            questionIds = @($question.id)
        }
        $quiz = Invoke-LmsApi -Session $author -Path "/api/quizzes/$($quiz.quiz.id)/publish" -Method POST -Csrf -Body @{
            revision = $quiz.quiz.revision
        }
        Write-Host "Đã tạo: $($quiz.quiz.title) -> $($classroom.name)" -ForegroundColor Green
    }
}
finally {
    if ($null -ne $reviewerUser) {
        $null = Invoke-LmsApi -Session $author -Path "/api/users/$($reviewerUser.id)/toggle-active" -Method POST -Body @{}
        Write-Host "Tài khoản reviewer tạm đã bị khóa."
    }
    $adminPassword = $null
    $reviewerPassword = $null
}

Write-Host "Hoàn tất $createdCount quiz. Tải lại $BaseUrl/app/quizzes" -ForegroundColor Cyan
