param(
    [Parameter(Mandatory = $true)][string]$Archive,
    [string]$TargetDatabase,
    [switch]$ConfirmRestore,
    [switch]$AllowConfiguredDatabase
)

$ErrorActionPreference = "Stop"
if (-not $ConfirmRestore) { throw "Restore sẽ ghi đè collection trùng tên. Chạy lại với -ConfirmRestore." }
if (-not (Test-Path $Archive -PathType Leaf)) { throw "Không tìm thấy file backup: $Archive" }

function Import-DotEnv {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return }
    foreach ($line in Get-Content $Path) {
        if ($line -match '^\s*#' -or $line -notmatch '^\s*([^=]+?)\s*=\s*(.*)\s*$') { continue }
        $name = $Matches[1]
        $value = $Matches[2].Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }
        if (-not [Environment]::GetEnvironmentVariable($name)) { [Environment]::SetEnvironmentVariable($name, $value) }
    }
}

Import-DotEnv (Join-Path $PSScriptRoot "..\.env")
$uri = [Environment]::GetEnvironmentVariable("MongoDb__ConnectionString")
$database = [Environment]::GetEnvironmentVariable("MongoDb__DatabaseName")
if (-not $uri -or -not $database) { throw "Thiếu MongoDb__ConnectionString hoặc MongoDb__DatabaseName trong môi trường/.env." }
if (-not (Get-Command mongorestore -ErrorAction SilentlyContinue)) { throw "Chưa cài MongoDB Database Tools (mongorestore)." }
$target = if ($TargetDatabase) { $TargetDatabase } else { "$database-RestoreTest" }
if ($target -notmatch '^[A-Za-z0-9_-]+$') { throw "Tên database đích không hợp lệ." }
if ($target -eq $database -and -not $AllowConfiguredDatabase) {
    throw "Mặc định không restore đè database đang cấu hình. Thêm -AllowConfiguredDatabase nếu đã dừng ứng dụng và thực sự muốn làm việc này."
}

& mongorestore "--uri=$uri" "--archive=$Archive" --gzip --drop `
    "--nsInclude=$database.*" "--nsFrom=$database.*" "--nsTo=$target.*"
if ($LASTEXITCODE -ne 0) { throw "mongorestore thất bại với mã $LASTEXITCODE." }
Write-Host "Đã restore database thử nghiệm $target từ $Archive"
