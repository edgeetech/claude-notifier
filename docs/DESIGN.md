# Design

## Goals

1. Surface Claude Code permission prompts with sound, motion, and a click-to-focus shortcut.
2. Never block, hang, or take destructive action on the user's terminal tabs.
3. Work for any number of concurrent Claude sessions across any number of WT tabs.

## Non-goals

- Surfacing every Notification event (idle pings are explicitly filtered out).
- Modifying the user's terminal beyond bringing it to the foreground.
- Supporting non-Windows platforms.

## Components

```
hook  (~50 ms PowerShell)         app  (long-running .NET 8 WPF tray)
─────────────────────────         ──────────────────────────────────────
read stdin                        FileSystemWatcher on events-*.jsonl
append JSON line                  parse → filter → dedupe
exit                              sound + overlay + toast
                                  click → UIA tab focus
```

The hook intentionally owns no UI — that was the source of every prior failure
mode (focus theft, killed tabs, fresh-shell spawns). Decoupling the emitter
from the listener means hook misbehavior can at worst write a malformed JSON
line; the app catches the parse error and moves on.

## Event flow

1. Claude Code fires `Notification` with `{ message, session_id, ... }` on stdin.
2. Hook script reads stdin, captures `$PID`, `$env:WT_SESSION`, `(Get-Location)`,
   `[Console]::Title`, wraps the payload, appends one JSON line to
   `~/.claude/notify/events-YYYYMMDD.jsonl`, and exits.
3. `FileSystemWatcher` in the tray app sees the size change, reads from the
   last byte-offset for that file, parses each new line.
4. `EventFilter` drops anything whose message doesn't match
   `^Claude needs your permission to use ` (idle pings die here), then
   deduplicates by `(session_id, tool_name)` within `DedupeWindowMs`.
5. Surviving events trigger sound + overlay + (optional) native toast.

## Tab focus (UIA)

`UiaTabFocus` looks up the originating tab in this order:

1. Exact match: `tab.Current.Name == evt.TabTitle` (the `[Console]::Title`
   captured at hook fire-time).
2. Substring: tab name contains the first 8 chars of the WT session GUID.
3. Substring: tab name contains the basename of `cwd`.

If any strategy hits, the app:
- Restores the window if iconic.
- Calls `SetForegroundWindow` **only on verified `WindowsTerminal.exe`
  processes** (`Process.GetProcessesByName("WindowsTerminal")`).
- Invokes `SelectionItemPattern.Select()` on the tab element (falls back
  to `InvokePattern.Invoke()`).

It **never** calls `wt.exe focus-tab` (which would spawn a new tab/window on
index mismatch), never uses `AttachThreadInput`, never touches an hwnd whose
owning process isn't WindowsTerminal.

## Safety guarantees

- The hook cannot block Claude Code: it's `async: true`, exits in well under
  100 ms, and never depends on the app being running.
- The app cannot break the hook: a crashed app just stops processing the
  JSONL file; events queue and resume when the app restarts.
- The app cannot accidentally close terminal tabs: there is no API call to
  WT or to any window other than verified WT hwnds for foreground switching.
- A kill switch (`$env:CLAUDE_TOAST_DISABLE`) and a tray toggle let users
  silence the system in seconds without uninstalling.

## Configuration

`~/.claude/notify/config.json` is read at startup, written on every tray
toggle:

```json
{
  "Enabled": true,
  "PlaySound": true,
  "ShowOverlay": true,
  "ShowToast": false,
  "ClickFocusesTab": true,
  "DedupeWindowMs": 3000,
  "SoundFile": "%WINDIR%\\Media\\Windows Notify.wav",
  "OverlayStyle": "bubble",
  "SnoozedUntilUtc": null
}
```

## Why a separate app instead of a long-lived hook script

A long-running PowerShell hook would have to:

- Pump a message loop for the lifetime of the toast (NotifyIcon requires
  `DoEvents` ticks).
- Own the click handler's runspace — and when the script ended for any
  reason, Windows would re-invoke the registered AUMID to handle the
  click, which on bare systems falls back to launching `powershell.exe`.
- Manage focus from inside a child process that may not have the
  foreground-stealing right.

Decoupling fixes all three: the app holds the message loop, owns the click
handler with full process lifetime, and the hook stays minimal.

## Forensic notes on a prior failure

An earlier version killed three Windows Terminal tabs when a click handler
called, in sequence, `AttachThreadInput → BringWindowToTop →
ShowWindow(hwnd, SW_SHOW) → SetForegroundWindow` on an hwnd derived from
a parent-process walk. The likely root cause: the walked hwnd was a
different WT window's; the foreground-stealing dance plus a misfired
`wt focus-tab` (out-of-range tab index) opened a default-profile tab, which
visually replaced the active session. The current design refuses to call
any of those APIs unless the hwnd's owning process name is literally
`WindowsTerminal.exe`, and never calls `wt.exe` at all.
