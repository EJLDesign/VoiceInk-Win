using System.IO;
using System.Text.Json;
using VoiceInkWin.Models;

namespace VoiceInkWin.Services;

public class SettingsService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoiceInk");

    private static readonly string SettingsFile = Path.Combine(AppDataFolder, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AppSettings Settings { get; private set; } = new();

    public static string GetAppDataFolder() => AppDataFolder;
    public static string GetModelsFolder() => Path.Combine(AppDataFolder, "Models");

    public void Load()
    {
        Directory.CreateDirectory(AppDataFolder);
        Directory.CreateDirectory(GetModelsFolder());

        if (File.Exists(SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(SettingsFile);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            catch
            {
                Settings = new AppSettings();
            }
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDataFolder);
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsFile, json);
    }
}
