# Claude Notifier — uninstaller.
#
# Run:
#   iwr -useb https://raw.githubusercontent.com/edgeetech/claude-notifier/main/uninstall.ps1 | iex

[CmdletBinding()]
param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA "ClaudeNotifier"),
    [switch]$KeepEvents,
    [switch]$KeepConfig
)

$ErrorActionPreference = 'Continue'

function Write-Step($msg) { Write-Host "[claude-notifier] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn2($m)  { Write-Host "  ! $m" -ForegroundColor Yellow }

Write-Step "Stopping running instance"
Get-Process ClaudeNotifier -EA SilentlyContinue | Stop-Process -Force -EA SilentlyContinue
Write-Ok "Stopped"

Write-Step "Removing autostart"
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
                    -Name "ClaudeNotifier" -EA SilentlyContinue
Write-Ok "HKCU Run key removed (if it existed)"

Write-Step "Removing Notification hook from Claude settings.json"
$claudeDir = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR }
             else { Join-Path $env:USERPROFILE ".claude" }
$settingsPath = Join-Path $claudeDir "settings.json"
if (Test-Path $settingsPath) {
    $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if ($settings.hooks -and $settings.hooks.Notification) {
        $kept = @()
        foreach ($block in @($settings.hooks.Notification)) {
            $drop = $false
            foreach ($h in @($block.hooks)) {
                if ($h.command -and $h.command -match "claude-notify\.ps1") { $drop = $true; break }
            }
            if (-not $drop) { $kept += $block }
        }
        $settings.hooks.Notification = $kept
        $settings | ConvertTo-Json -Depth 12 | Set-Content $settingsPath -Encoding utf8
        Write-Ok "Hook entry removed"
    }
} else {
    Write-Warn2 "settings.json not found"
}

Write-Step "Removing hook script"
$hookDst = Join-Path $claudeDir "scripts\claude-notify.ps1"
if (Test-Path $hookDst) { Remove-Item -Force $hookDst; Write-Ok "Deleted $hookDst" }
else { Write-Warn2 "Hook script not present" }

Write-Step "Removing install dir"
if (Test-Path $InstallDir) { Remove-Item -Recurse -Force $InstallDir; Write-Ok "Deleted $InstallDir" }
else { Write-Warn2 "Install dir not present" }

$claudeRoot = if ($env:CLAUDE_CONFIG_DIR) { $env:CLAUDE_CONFIG_DIR } else { Join-Path $env:USERPROFILE ".claude" }
$eventDir   = Join-Path $claudeRoot "notify"

if (-not $KeepEvents) {
    Write-Step "Removing event logs"
    if (Test-Path $eventDir) {
        Get-ChildItem $eventDir -Filter "events-*.jsonl" -EA SilentlyContinue | Remove-Item -Force -EA SilentlyContinue
        Remove-Item -Force (Join-Path $eventDir "notifier.log") -EA SilentlyContinue
        Write-Ok "Event logs deleted (use -KeepEvents to skip)"
    }
}
if (-not $KeepConfig) {
    $cfg = Join-Path $eventDir "config.json"
    if (Test-Path $cfg) { Remove-Item -Force $cfg; Write-Ok "config.json deleted (use -KeepConfig to skip)" }
}

Write-Host ""
Write-Host "Uninstalled." -ForegroundColor Green
