using System.Text.Json.Serialization;

namespace VoiceInkWin.Models;

public class Mode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public bool IsBuiltIn { get; set; } = false;

    public static List<Mode> GetBuiltInModes() =>
    [
        new() { Name = "Raw", SystemPrompt = "", IsBuiltIn = true },
        new() { Name = "Clean", SystemPrompt = "Fix grammar, punctuation, and remove filler words (um, uh, like, you know). Keep the meaning and tone identical. Do not add or remove content. Return only the cleaned text with no explanation.", IsBuiltIn = true },
        new() { Name = "Email", SystemPrompt = "Format this dictated text as a professional email. Add a greeting and sign-off if not already present. Fix grammar and punctuation. Keep the core message intact. Return only the formatted email with no explanation.", IsBuiltIn = true },
        new() { Name = "Slack", SystemPrompt = "Format this dictated text for a Slack message. Keep it casual and concise. Use line breaks instead of paragraphs. Fix grammar but keep informal tone. Return only the formatted message with no explanation.", IsBuiltIn = true },
        new() { Name = "Notes", SystemPrompt = "Convert this dictated text into bullet-point notes. Extract key points and organize them logically. Use concise language. Return only the bullet points with no explanation.", IsBuiltIn = true },
        new() { Name = "Code Comment", SystemPrompt = "Format this dictated text as a code comment block. Be technical and precise. Remove filler words and conversational language. Return only the comment text (without comment syntax) with no explanation.", IsBuiltIn = true },
    ];
}
