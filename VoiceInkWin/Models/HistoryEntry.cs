namespace VoiceInkWin.Models;

public class HistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = "";
    public string ModeName { get; set; } = "Raw";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
