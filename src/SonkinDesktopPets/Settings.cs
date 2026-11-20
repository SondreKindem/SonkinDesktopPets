using System.Text.Json;
using System.Text.Json.Serialization;

namespace SonkinDesktopPets;

internal sealed class Settings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SonkinDesktopPets",
        "settings.json");

    /// <summary>Device name of the screen the cat is confined to, or null to roam every screen.</summary>
    public string? ScreenDevice { get; set; }

    public bool Mischief { get; set; } = true;

    public bool Notes { get; set; } = true;

    public int Scale { get; set; } = 3;

    /// <summary>True to fly behind every window, just above the wallpaper, instead of on top.</summary>
    public bool Background { get; set; }

    /// <summary>Whether the cat is out.</summary>
    public bool Cat { get; set; } = true;

    public int SheepCount { get; set; } = 1;

    /// <summary>The landscape drawn along the bottom of the screen, if any.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Scene Scene { get; set; } = Scene.None;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath));
                if (loaded is not null)
                {
                    loaded.Scale = Math.Clamp(loaded.Scale, 2, 8);
                    loaded.SheepCount = Math.Clamp(loaded.SheepCount, 0, SheepFlock.MaxSheep);
                    if (!Enum.IsDefined(loaded.Scene)) loaded.Scene = Scene.None;
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken settings file shouldn't keep the cat grounded; start from defaults.
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings just won't persist this time.
        }
    }
}
