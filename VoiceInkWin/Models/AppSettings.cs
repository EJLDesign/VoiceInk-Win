using System.Text.Json.Serialization;

namespace VoiceInkWin.Models;

public class AppSettings
{
    // General
    public string RecordingMode { get; set; } = "toggle"; // "toggle" or "pushToTalk"
    public bool LaunchAtLogin { get; set; } = false;
    public bool PlaySoundOnComplete { get; set; } = true;

    // Hotkey
    public int HotkeyKeyCode { get; set; } = 0;
    public int HotkeyModifiers { get; set; } = 0;

    // Model / Transcription
    public string SelectedModelName { get; set; } = "ggml-base.en.bin";
    public string TranscriptionLanguage { get; set; } = "en";
    public bool TranslateToEnglish { get; set; } = false;
    public string VocabularyHints { get; set; } = "";

    // AI Processing
    public string AiProvider { get; set; } = "none"; // "none", "anthropic", "ollama"
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModelName { get; set; } = "llama3";
    public string SelectedModeName { get; set; } = "Raw";

    // Audio
    public string AudioInputDeviceId { get; set; } = "";
    public float SilenceThreshold { get; set; } = 0.003f;
    public float MaxRecordingDuration { get; set; } = 120.0f;

    // UI
    public bool ShowFloatingHud { get; set; } = true;
}
