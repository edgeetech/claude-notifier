using System;
using System.Linq;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ClaudeNotifier;

public static class ToastService
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ClaudeEvent> _pending = new();

    public static void Show(ClaudeEvent evt)
    {
        try
        {
            var tabTag = !string.IsNullOrEmpty(evt.WtSession) && evt.WtSession.Length >= 8
                ? evt.WtSession.Substring(0, 8) : "";

            // Stash event in a small lookup so click handler can recover it
            var key = Guid.NewGuid().ToString("N");
            _pending[key] = evt;

            var builder = new ToastContentBuilder()
                .AddArgument("action", "focus")
                .AddArgument("key", key)
                .AddText("Claude needs approval")
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
                    _pending.TryRemove(key, out var evt))
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        UiaTabFocus.Focus(evt);
                    });
                }
            }
        }
        catch (Exception ex)
        {
            App.Log("Toast activation handler failed: " + ex.Message);
        }
    }
}
