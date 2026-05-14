using System;
using System.Collections.Concurrent;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ClaudeNotifier;

public static class ToastService
{
    // Pending entries expire after this duration even if the toast was never
    // clicked (user dismissed or let it sit in Action Center).
    private static readonly TimeSpan PendingTtl = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, (ClaudeEvent evt, DateTime expires)> _pending = new();

    public static void Show(ClaudeEvent evt)
    {
        try
        {
            SweepExpired();

            var tabTag = !string.IsNullOrEmpty(evt.WtSession) && evt.WtSession.Length >= 8
                ? evt.WtSession.Substring(0, 8) : "";

            var key = Guid.NewGuid().ToString("N");
            _pending[key] = (evt, DateTime.UtcNow + PendingTtl);

            var title = evt.Kind == "idle" ? "Claude is waiting" : "Claude needs approval";
            var builder = new ToastContentBuilder()
                .AddArgument("action", "focus")
                .AddArgument("key", key)
                .AddText(title)
                .AddText(evt.Message);

            if (!string.IsNullOrEmpty(evt.Cwd))
                builder.AddText($"{evt.Cwd}  [tab {tabTag}]");

            builder.Show(toast =>
            {
                toast.ExpirationTime = DateTime.Now.AddMinutes(2);
                toast.Tag = (evt.SessionId ?? evt.WtSession ?? evt.Pid.ToString());
                toast.Group = "claude-approval";
            });
        }
        catch (Exception ex)
        {
            App.Log("Toast failed: " + ex.Message);
        }
    }

    public static void OnToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            var args = ToastArguments.Parse(e.Argument);
            if (args.TryGetValue("action", out var action) && action == "focus")
            {
                args.TryGetValue("key", out var key);
                App.Log($"Toast clicked: key={key}");
                if (App.Config.ClickFocusesTab && !string.IsNullOrEmpty(key) &&
                    _pending.TryRemove(key, out var entry))
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        UiaTabFocus.Focus(entry.evt);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            App.Log("Toast activation handler failed: " + ex.Message);
        }
    }

    private static void SweepExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _pending)
        {
            if (kv.Value.expires < now) _pending.TryRemove(kv.Key, out _);
        }
    }
}
