using System;
using System.IO;
using System.Text.Json;

namespace ClaudeNotifier;

public class AppConfig
{
    public bool Enabled { get; set; } = true;
    public bool PlaySound { get; set; } = true;
    public bool ShowOverlay { get; set; } = true;
    public bool ShowToast { get; set; } = false;        // off by default; user disliked duplicate
    public bool ClickFocusesTab { get; set; } = true;
    public int DedupeWindowMs { get; set; } = 3000;
    public string SoundFile { get; set; } = @"%WINDIR%\Media\Windows Notify.wav";

    /// <summary>bubble | ledbar | particles</summary>
    public string OverlayStyle { get; set; } = "bubble";

    public DateTime? SnoozedUntilUtc { get; set; } = null;

    private static string Path => System.IO.Path.Combine(App.EventDir, "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path);
                return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            }
        }
        catch (Exception ex) { App.Log("Config load failed: " + ex.Message); }
        var def = new AppConfig();
        def.Save();
        return def;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path, json);
        }
        catch (Exception ex) { App.Log("Config save failed: " + ex.Message); }
    }

    public bool IsSnoozedNow()
    {
        return SnoozedUntilUtc.HasValue && SnoozedUntilUtc.Value > DateTime.UtcNow;
    }
}
