using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace ClaudeNotifier;

public static class OverlayService
{
    private static readonly object _lock = new();
    private static readonly List<Window> _open = new();

    public static void Show(ClaudeEvent evt)
    {
        try
        {
            Window w = App.Config.OverlayStyle?.ToLowerInvariant() switch
            {
                "ledbar"    => new LedBarOverlay(evt),
                "particles" => new ParticleOverlay(evt),
                _           => new OverlayWindow(evt)
            };

            lock (_lock) _open.Add(w);
            w.Closed += (s, e) => { lock (_lock) _open.Remove(w); };

            w.Show();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                try { if (w.IsVisible && w is IOverlayWindow io) io.BeginDismiss(); } catch { }
            };
            timer.Start();
        }
        catch (Exception ex) { App.Log("Overlay failed: " + ex.Message); }
    }

    public static void DismissAll()
    {
        Window[] snap;
        lock (_lock) snap = _open.ToArray();
        foreach (var w in snap)
        {
            try
            {
                if (w is IOverlayWindow io && w.IsVisible) io.BeginDismiss();
                else if (w.IsVisible) w.Close();
            }
            catch { }
        }
    }
}
