using System.IO;
using System.Text.Json;
using VoiceInkWin.Models;

namespace VoiceInkWin.Services;

public class ModeManager
{
    private static readonly string CustomModesFile = Path.Combine(
        SettingsService.GetAppDataFolder(), "custom-modes.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<Mode> _builtInModes = Mode.GetBuiltInModes();
    private List<Mode> _customModes = new();

    public IReadOnlyList<Mode> AllModes => _builtInModes.Concat(_customModes).ToList();

    public void Load()
    {
        if (File.Exists(CustomModesFile))
        {
            try
            {
                var json = File.ReadAllText(CustomModesFile);
                _customModes = JsonSerializer.Deserialize<List<Mode>>(json, JsonOptions) ?? new();
            }
            catch
            {
                _customModes = new();
            }
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_customModes, JsonOptions);
        File.WriteAllText(CustomModesFile, json);
    }

    public Mode? GetMode(string name) =>
        AllModes.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void AddCustomMode(string name, string systemPrompt)
    {
        _customModes.Add(new Mode
        {
            Name = name,
            SystemPrompt = systemPrompt,
            IsBuiltIn = false
        });
        Save();
    }

    public void UpdateCustomMode(Guid id, string name, string systemPrompt)
    {
        var mode = _customModes.FirstOrDefault(m => m.Id == id);
        if (mode != null)
        {
            mode.Name = name;
            mode.SystemPrompt = systemPrompt;
            Save();
        }
    }

    public void DeleteCustomMode(Guid id)
    {
        _customModes.RemoveAll(m => m.Id == id);
        Save();
    }
}
