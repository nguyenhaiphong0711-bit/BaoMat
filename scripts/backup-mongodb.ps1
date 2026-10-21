param(
    [string]$OutputDirectory = "backups"
)

$ErrorActionPreference = "Stop"

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
if (-not (Get-Command mongodump -ErrorAction SilentlyContinue)) { throw "Chưa cài MongoDB Database Tools (mongodump)." }

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$archive = Join-Path $OutputDirectory "$database-$stamp.archive.gz"
& mongodump "--uri=$uri" "--db=$database" "--archive=$archive" --gzip
if ($LASTEXITCODE -ne 0) { throw "mongodump thất bại với mã $LASTEXITCODE." }
Write-Host "Đã tạo backup: $archive"
