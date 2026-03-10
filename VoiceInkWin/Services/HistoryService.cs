using System.IO;
using System.Text.Json;
using VoiceInkWin.Models;

namespace VoiceInkWin.Services;

public class HistoryService
{
    private const int MaxEntries = 20;

    private static readonly string HistoryFile = Path.Combine(
        SettingsService.GetAppDataFolder(), "history.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<HistoryEntry> _entries = new();

    public IReadOnlyList<HistoryEntry> Entries => _entries.AsReadOnly();

    public void Load()
    {
        if (File.Exists(HistoryFile))
        {
            try
            {
                var json = File.ReadAllText(HistoryFile);
                _entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOptions) ?? new();
            }
            catch
            {
                _entries = new();
            }
        }
    }

    public void AddEntry(string text, string modeName)
    {
        _entries.Insert(0, new HistoryEntry
        {
            Text = text,
            ModeName = modeName,
            Timestamp = DateTime.UtcNow
        });

        if (_entries.Count > MaxEntries)
            _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);

        Save();
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }

    public string ExportToText()
    {
        var lines = _entries.Select(e =>
            $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] ({e.ModeName}) {e.Text}");
        return string.Join(Environment.NewLine, lines);
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_entries, JsonOptions);
            File.WriteAllText(HistoryFile, json);
        }
        catch { }
    }
}
