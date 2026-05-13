# Claude Notifier — local installer.
# Runs after the project has been built (`dotnet build -c Release`).
# Idempotent. Re-running upgrades the install in place.
#
# Usage:
#   pwsh ./install-local.ps1                # use defaults
#   pwsh ./install-local.ps1 -NoAutostart    # skip HKCU Run key
#   pwsh ./install-local.ps1 -NoStart        # don't launch the exe at the end

[CmdletBinding()]
param(
    [switch]$NoAutostart,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'

function Write-Step($msg) { Write-Host "[claude-notifier] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn2($m)  { Write-Host "  ! $m" -ForegroundColor Yellow }

# --- 1. Locate built exe ---
$exe = Join-Path $PSScriptRoot "bin\Release\net8.0-windows10.0.19041.0\ClaudeNotifier.exe"
if (-not (Test-Path $exe)) {
    Write-Error "ClaudeNotifier.exe not found at $exe`nRun: dotnet build -c Release"
    exit 1
}
Write-Step "Using exe: $exe"

# --- 2. Resolve Claude config dir ---
$claudeDir = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR }
             else { Join-Path $env:USERPROFILE ".claude" }
$settingsPath = Join-Path $claudeDir "settings.json"

if (-not (Test-Path $claudeDir)) {
    Write-Warn2 "Claude config dir not found at $claudeDir — creating it"
    New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null
}

# --- 3. Copy hook script ---
$scriptsDir = Join-Path $claudeDir "scripts"
if (-not (Test-Path $scriptsDir)) { New-Item -ItemType Directory -Path $scriptsDir | Out-Null }
$hookSrc = Join-Path $PSScriptRoot "hook\claude-notify.ps1"
$hookDst = Join-Path $scriptsDir "claude-notify.ps1"
Copy-Item -Force $hookSrc $hookDst
Write-Ok "Hook script installed at $hookDst"

# --- 4. Patch settings.json to register Notification hook ---
$hookCmd = "& `"$hookDst`""
$hookEntry = [pscustomobject]@{
    hooks = @(
        [pscustomobject]@{
            type    = "command"
            command = $hookCmd
            shell   = "powershell"
            async   = $true
        }
    )
}

if (-not (Test-Path $settingsPath)) {
    # Create minimal settings.json
    $json = [pscustomobject]@{
        hooks = [pscustomobject]@{
            Notification = @($hookEntry)
        }
    }
    $json | ConvertTo-Json -Depth 8 | Set-Content $settingsPath -Encoding utf8
    Write-Ok "Created $settingsPath with Notification hook"
} else {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if (-not $settings.hooks)        { $settings | Add-Member hooks ([pscustomobject]@{}) -Force }
    if (-not $settings.hooks.Notification) {
        $settings.hooks | Add-Member Notification @() -Force
    }

    # Replace any existing claude-notify.ps1 entries to keep things idempotent
    $existing = @($settings.hooks.Notification)
    $cleaned  = @()
    foreach ($block in $existing) {
        $keep = $true
        foreach ($h in @($block.hooks)) {
            if ($h.command -and $h.command -match "claude-notify\.ps1") { $keep = $false; break }
        }
        if ($keep) { $cleaned += $block }
    }
    $cleaned += $hookEntry
    $settings.hooks.Notification = $cleaned

    $settings | ConvertTo-Json -Depth 12 | Set-Content $settingsPath -Encoding utf8
    Write-Ok "Patched $settingsPath (Notification hook)"
}

# --- 5. Autostart ---
if (-not $NoAutostart) {
    $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
    Set-ItemProperty -Path $runKey -Name "ClaudeNotifier" -Value "`"$exe`""
    Write-Ok "Autostart registered at HKCU\...\Run\ClaudeNotifier"
} else {
    Write-Warn2 "Autostart skipped (-NoAutostart)"
}

# --- 6. Kill any running instance and launch fresh ---
Get-Process ClaudeNotifier -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Start-Sleep -Milliseconds 300

if (-not $NoStart) {
    Start-Process -FilePath $exe -WindowStyle Hidden
    Write-Ok "ClaudeNotifier launched — look for the spark icon in your system tray"
} else {
    Write-Warn2 "App not started (-NoStart). Launch manually: $exe"
}

Write-Host ""
Write-Host "Install complete." -ForegroundColor Green
Write-Host "Right-click the tray icon → Test notification to verify." -ForegroundColor Green
