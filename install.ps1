# Claude Notifier — one-liner bootstrap installer.
#
# Run:
#   iwr -useb https://raw.githubusercontent.com/edgeetech/claude-notifier/main/install.ps1 | iex
#
# What it does:
#   1. Verifies prerequisites (Windows, .NET 8 SDK, git)
#   2. Clones (or pulls) edgeetech/claude-notifier into %LOCALAPPDATA%\ClaudeNotifier
#   3. Builds the WPF app in Release mode
#   4. Delegates to install-local.ps1 for hook + settings + autostart wiring

[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "ClaudeNotifier"),
    [string]$Branch     = "main",
    [switch]$NoAutostart,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "[claude-notifier] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Err2($msg) { Write-Host "  ✗ $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════╗" -ForegroundColor Yellow
Write-Host "║   Claude Notifier — installing                       ║" -ForegroundColor Yellow
Write-Host "╚══════════════════════════════════════════════════════╝" -ForegroundColor Yellow
Write-Host ""

# --- 1. Prereqs ---
Write-Step "Checking prerequisites"

if ($PSVersionTable.Platform -and $PSVersionTable.Platform -ne 'Win32NT') {
    Write-Err2 "Windows only."
    exit 1
}

$git = Get-Command git -EA SilentlyContinue
if (-not $git) {
    Write-Err2 "git not found. Install Git for Windows: https://git-scm.com/download/win"
    exit 1
}
Write-Ok "git: $($git.Source)"

$dotnet = Get-Command dotnet -EA SilentlyContinue
if (-not $dotnet) {
    Write-Err2 ".NET SDK not found. Install .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}
$sdks = & dotnet --list-sdks
$has8 = $sdks | Where-Object { $_ -match '^8\.' }
if (-not $has8) {
    Write-Err2 ".NET 8 SDK not found. Installed SDKs:`n$($sdks -join "`n")"
    Write-Err2 "Install: https://dotnet.microsoft.com/download/dotnet/8.0"
    exit 1
}
Write-Ok ".NET 8 SDK present"

# --- 2. Clone or update ---
Write-Step "Fetching source to $InstallDir"
$repoUrl = "https://github.com/edgeetech/claude-notifier.git"

if (Test-Path (Join-Path $InstallDir ".git")) {
    Push-Location $InstallDir
    try {
        & git fetch origin $Branch | Out-Null
        & git reset --hard "origin/$Branch" | Out-Null
        Write-Ok "Updated to origin/$Branch"
    } finally { Pop-Location }
} else {
    if (Test-Path $InstallDir) {
        Write-Err2 "$InstallDir exists but is not a git repo. Delete it or pass -InstallDir to a fresh path."
        exit 1
    }
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
    & git clone --branch $Branch --depth 1 $repoUrl $InstallDir | Out-Null
    Write-Ok "Cloned $repoUrl"
}

# --- 3. Build ---
Write-Step "Building (dotnet build -c Release)"
Push-Location $InstallDir
try {
    & dotnet build -c Release --nologo --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Err2 "Build failed."
        exit $LASTEXITCODE
    }
    Write-Ok "Build OK"
} finally { Pop-Location }

# --- 4. Run local installer ---
Write-Step "Wiring hook + settings + autostart"
$localInstaller = Join-Path $InstallDir "install-local.ps1"
$args = @()
if ($NoAutostart) { $args += '-NoAutostart' }
if ($NoStart)     { $args += '-NoStart' }
& $localInstaller @args

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "Source: $InstallDir" -ForegroundColor DarkGray
Write-Host "Logs:   $env:USERPROFILE\.claude\notify\notifier.log" -ForegroundColor DarkGray
