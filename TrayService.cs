using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ClaudeNotifier;

public class TrayService : IDisposable
{
    private NotifyIcon? _tray;
    private readonly Dictionary<string, ToolStripMenuItem> _styleItems = new();

    public void Start()
    {
        _tray = new NotifyIcon
        {
            Icon = IconFactory.Create(32),
            Text = "Claude Notifier",
            Visible = true
        };

        var menu = new ContextMenuStrip();

        var enabledItem = new ToolStripMenuItem("Enabled") { Checked = App.Config.Enabled, CheckOnClick = true };
        enabledItem.CheckedChanged += (s, e) => { App.Config.Enabled = enabledItem.Checked; App.Config.Save(); };

        var soundItem = new ToolStripMenuItem("Play sound") { Checked = App.Config.PlaySound, CheckOnClick = true };
        soundItem.CheckedChanged += (s, e) => { App.Config.PlaySound = soundItem.Checked; App.Config.Save(); };

        var overlayItem = new ToolStripMenuItem("Show overlay") { Checked = App.Config.ShowOverlay, CheckOnClick = true };
        overlayItem.CheckedChanged += (s, e) => { App.Config.ShowOverlay = overlayItem.Checked; App.Config.Save(); };

        // Overlay style submenu
        var styleMenu = new ToolStripMenuItem("Overlay style");
        foreach (var name in new[] { "bubble", "ledbar", "particles" })
        {
            var item = new ToolStripMenuItem(name) { Checked = App.Config.OverlayStyle == name, CheckOnClick = false };
            string captured = name;
            item.Click += (s, e) =>
            {
                App.Config.OverlayStyle = captured;
                App.Config.Save();
                foreach (var kv in _styleItems) kv.Value.Checked = kv.Key == captured;
            };
            _styleItems[name] = item;
            styleMenu.DropDownItems.Add(item);
        }

        var toastItem = new ToolStripMenuItem("Show Windows toast") { Checked = App.Config.ShowToast, CheckOnClick = true };
        toastItem.CheckedChanged += (s, e) => { App.Config.ShowToast = toastItem.Checked; App.Config.Save(); };

        var clickFocusItem = new ToolStripMenuItem("Click focuses tab") { Checked = App.Config.ClickFocusesTab, CheckOnClick = true };
        clickFocusItem.CheckedChanged += (s, e) => { App.Config.ClickFocusesTab = clickFocusItem.Checked; App.Config.Save(); };

        // Snooze submenu
        var snoozeMenu = new ToolStripMenuItem("Snooze");
        foreach (var (label, mins) in new (string, int)[] { ("5 minutes", 5), ("15 minutes", 15), ("1 hour", 60), ("4 hours", 240) })
        {
            int m = mins;
            var it = new ToolStripMenuItem(label);
            it.Click += (s, e) =>
            {
                App.Config.SnoozedUntilUtc = DateTime.UtcNow.AddMinutes(m);
                App.Config.Save();
                UpdateSnoozeLabel(snoozeMenu);
            };
            snoozeMenu.DropDownItems.Add(it);
        }
        var unsnoozeItem = new ToolStripMenuItem("Cancel snooze");
        unsnoozeItem.Click += (s, e) =>
        {
            App.Config.SnoozedUntilUtc = null;
            App.Config.Save();
            UpdateSnoozeLabel(snoozeMenu);
        };
        snoozeMenu.DropDownItems.Add(new ToolStripSeparator());
        snoozeMenu.DropDownItems.Add(unsnoozeItem);
        UpdateSnoozeLabel(snoozeMenu);

        var dismissAllItem = new ToolStripMenuItem("Dismiss all open overlays");
        dismissAllItem.Click += (s, e) => OverlayService.DismissAll();

        var testItem = new ToolStripMenuItem("Test notification");
        testItem.Click += (s, e) =>
        {
            var fake = new ClaudeEvent(
                Ts: DateTime.Now, Pid: 0,
                WtSession: Environment.GetEnvironmentVariable("WT_SESSION") ?? "00000000-0000-0000-0000-000000000000",
                Cwd: Environment.CurrentDirectory,
                Message: "Claude needs your permission to use Bash",
                SessionId: "test", ToolName: "Bash",
                TabTitle: null);
            EventFilter.HandleEvent(fake);
        };

        var openLogItem = new ToolStripMenuItem("Open log");
        openLogItem.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("notepad.exe", App.LogPath) { UseShellExecute = true }); }
            catch { }
        };

        var openDirItem = new ToolStripMenuItem("Open events folder");
        openDirItem.Click += (s, e) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", App.EventDir) { UseShellExecute = true }); }
            catch { }
        };

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();

        menu.Items.Add(enabledItem);
        menu.Items.Add(snoozeMenu);
        menu.Items.Add(dismissAllItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(soundItem);
        menu.Items.Add(overlayItem);
        menu.Items.Add(styleMenu);
        menu.Items.Add(toastItem);
        menu.Items.Add(clickFocusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(testItem);
        menu.Items.Add(openLogItem);
        menu.Items.Add(openDirItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quitItem);

        _tray.ContextMenuStrip = menu;
    }

    private static void UpdateSnoozeLabel(ToolStripMenuItem snoozeMenu)
    {
        if (App.Config.IsSnoozedNow())
        {
            var remaining = App.Config.SnoozedUntilUtc!.Value - DateTime.UtcNow;
            snoozeMenu.Text = $"Snooze (active, {(int)remaining.TotalMinutes}m left)";
        }
        else
        {
            snoozeMenu.Text = "Snooze";
        }
    }

    public void Dispose()
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
    }
}
