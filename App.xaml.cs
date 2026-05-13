using System;
using System.IO;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ClaudeNotifier;

public partial class App : System.Windows.Application
{
    public static string EventDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "notify");

    public static string LogPath { get; } = Path.Combine(EventDir, "notifier.log");

    public static AppConfig Config { get; private set; } = new();

    private TrayService? _tray;
    private EventWatcher? _watcher;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        Directory.CreateDirectory(EventDir);
        Config = AppConfig.Load();
        Log("ClaudeNotifier starting");

        // Wire toast-activated handler. App is single-instance-ish via AUMID.
        ToastNotificationManagerCompat.OnActivated += ToastService.OnToastActivated;

        _tray = new TrayService();
        _tray.Start();

        _watcher = new EventWatcher(EventDir);
        _watcher.OnEvent += EventFilter.HandleEvent;
        _watcher.Start();

        Log("Watching " + EventDir);
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Log("Shutdown");
        _watcher?.Dispose();
        _tray?.Dispose();
        ToastNotificationManagerCompat.Uninstall();
    }

    public static void Log(string msg)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:O}  {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
