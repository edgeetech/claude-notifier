using System;
using System.Collections.Generic;
using System.Windows;

namespace ClaudeNotifier;

public static class EventFilter
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, DateTime> _recent = new();

    public static void HandleEvent(ClaudeEvent evt)
    {
        if (!App.Config.Enabled) return;
        if (App.Config.IsSnoozedNow())
        {
            App.Log($"SNOOZED until {App.Config.SnoozedUntilUtc:O}; dropping event");
            return;
        }

        // Permission-only filter
        if (string.IsNullOrEmpty(evt.ToolName)) return;

        // Dedupe: same session + tool within window
        var key = $"{evt.SessionId ?? evt.WtSession ?? evt.Pid.ToString()}|{evt.ToolName}";
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            if (_recent.TryGetValue(key, out var last) &&
                (now - last).TotalMilliseconds < App.Config.DedupeWindowMs)
            {
                return;
            }
            _recent[key] = now;
            // Trim old entries
            if (_recent.Count > 200)
            {
                var cutoff = now.AddMinutes(-5);
                var stale = new List<string>();
                foreach (var kv in _recent) if (kv.Value < cutoff) stale.Add(kv.Key);
                foreach (var s in stale) _recent.Remove(s);
            }
        }

        App.Log($"FIRE  tool={evt.ToolName} wt={evt.WtSession} pid={evt.Pid} cwd={evt.Cwd}");

        // Hop to UI thread for sound/overlay/toast
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (App.Config.PlaySound) SoundService.Play();
                if (App.Config.ShowToast) ToastService.Show(evt);
                if (App.Config.ShowOverlay) OverlayService.Show(evt);
            }
            catch (Exception ex) { App.Log("Fire failed: " + ex.Message); }
        });
    }
}
