using System;
using System.IO;
using System.Media;

namespace ClaudeNotifier;

public static class SoundService
{
    public static void Play()
    {
        try
        {
            var path = Environment.ExpandEnvironmentVariables(App.Config.SoundFile);
            if (!File.Exists(path)) { SystemSounds.Exclamation.Play(); return; }
            using var sp = new SoundPlayer(path);
            sp.Play(); // async; returns immediately
        }
        catch (Exception ex)
        {
            App.Log("Sound failed: " + ex.Message);
            try { SystemSounds.Exclamation.Play(); } catch { }
        }
    }
}
