# Claude Code Notification hook — event emitter only.
# Appends one JSON line to %USERPROFILE%\.claude\notify\events-YYYYMMDD.jsonl and exits.
# All UI (sound, toast, overlay, click-to-focus) is handled by ClaudeNotifier.exe.
#
# Kill switch: set $env:CLAUDE_TOAST_DISABLE=1 to skip writes entirely.

if ($env:CLAUDE_TOAST_DISABLE) { exit 0 }
# Note: $IsWindows is PS 6+; in 5.1 it's $null. Skip OS check (hook only fires on Win anyway).

try {
    $raw = [Console]::In.ReadToEnd()
} catch { $raw = "" }

$dir = Join-Path $env:USERPROFILE ".claude\notify"
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$file = Join-Path $dir ("events-" + (Get-Date -Format "yyyyMMdd") + ".jsonl")

$tabTitle = $null
try { $tabTitle = [Console]::Title } catch { }

$evt = [pscustomobject]@{
    ts         = (Get-Date).ToString("o")
    pid        = $PID
    wt_session = $env:WT_SESSION
    cwd        = (Get-Location).Path
    tab_title  = $tabTitle
    payload    = $raw
}

$line = ($evt | ConvertTo-Json -Compress -Depth 4)
# Atomic-ish append via .NET StreamWriter in append mode
try {
    $sw = [System.IO.StreamWriter]::new($file, $true, [System.Text.UTF8Encoding]::new($false))
    $sw.WriteLine($line)
    $sw.Flush()
    $sw.Dispose()
} catch {
    # Fallback
    Add-Content -Path $file -Value $line -Encoding utf8
}
exit 0
