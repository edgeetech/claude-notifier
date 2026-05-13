using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace ClaudeNotifier;

public record ClaudeEvent(
    DateTime Ts,
    int Pid,
    string? WtSession,
    string? Cwd,
    string Message,
    string? SessionId,
    string? ToolName,
    string? TabTitle);

public class EventWatcher : IDisposable
{
    private readonly string _dir;
    private readonly FileSystemWatcher _fsw;
    private readonly Dictionary<string, long> _offsets = new();
    private readonly object _lock = new();

    public event Action<ClaudeEvent>? OnEvent;

    public EventWatcher(string dir)
    {
        _dir = dir;
        _fsw = new FileSystemWatcher(_dir, "events-*.jsonl")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };
        _fsw.Changed += (s, e) => SafeRead(e.FullPath);
        _fsw.Created += (s, e) => SafeRead(e.FullPath);
    }

    public void Start()
    {
        // Seed offsets at end of existing files so we don't replay history on startup.
        foreach (var f in Directory.GetFiles(_dir, "events-*.jsonl"))
        {
            try { _offsets[f] = new FileInfo(f).Length; } catch { }
        }
        _fsw.EnableRaisingEvents = true;
    }

    private void SafeRead(string path)
    {
        // Debounce — FSW fires multiple events per write
        Thread.Sleep(25);
        lock (_lock)
        {
            try
            {
                if (!File.Exists(path)) return;
                _offsets.TryGetValue(path, out var off);
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (off > fs.Length) off = 0; // file rotated/truncated
                fs.Seek(off, SeekOrigin.Begin);
                using var sr = new StreamReader(fs);
                string? line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line)) ParseAndEmit(line);
                }
                _offsets[path] = fs.Position;
            }
            catch (Exception ex)
            {
                App.Log($"Read failed {path}: {ex.Message}");
            }
        }
    }

    private void ParseAndEmit(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            string? message = null;
            string? sessionId = null;
            string? toolName = null;

            if (root.TryGetProperty("payload", out var payloadEl))
            {
                var payloadStr = payloadEl.GetString();
                if (!string.IsNullOrWhiteSpace(payloadStr))
                {
                    try
                    {
                        using var pdoc = JsonDocument.Parse(payloadStr);
                        var p = pdoc.RootElement;
                        if (p.TryGetProperty("message", out var m)) message = m.GetString();
                        if (p.TryGetProperty("session_id", out var s)) sessionId = s.GetString();
                    }
                    catch { }
                }
            }

            // Parse tool name from "Claude needs your permission to use <Tool>"
            if (!string.IsNullOrEmpty(message))
            {
                const string prefix = "Claude needs your permission to use ";
                if (message.StartsWith(prefix)) toolName = message.Substring(prefix.Length).Trim();
            }

            var evt = new ClaudeEvent(
                Ts: root.TryGetProperty("ts", out var ts) ? ts.GetDateTime() : DateTime.Now,
                Pid: root.TryGetProperty("pid", out var pid) ? pid.GetInt32() : 0,
                WtSession: root.TryGetProperty("wt_session", out var wts) ? wts.GetString() : null,
                Cwd: root.TryGetProperty("cwd", out var cwd) ? cwd.GetString() : null,
                Message: message ?? "",
                SessionId: sessionId,
                ToolName: toolName,
                TabTitle: root.TryGetProperty("tab_title", out var tt) ? tt.GetString() : null);

            OnEvent?.Invoke(evt);
        }
        catch (Exception ex)
        {
            App.Log("Parse failed: " + ex.Message);
        }
    }

    public void Dispose() { _fsw.Dispose(); }
}
