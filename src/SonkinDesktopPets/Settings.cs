using System.Text.Json;

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
