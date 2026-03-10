namespace VoiceInkWin.Models;

public class ModelInfo
{
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long FileSize { get; set; }
    public string DownloadUrl { get; set; } = "";
    public bool IsDownloaded { get; set; }

    public string FileSizeDisplay => FileSize switch
    {
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024L * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
    };

    public static List<ModelInfo> GetAvailableModels() =>
    [
        new() { FileName = "ggml-tiny.en.bin", DisplayName = "Tiny (English)", FileSize = 77_691_713, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin" },
        new() { FileName = "ggml-tiny.bin", DisplayName = "Tiny (Multilingual)", FileSize = 77_691_713, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin" },
        new() { FileName = "ggml-base.en.bin", DisplayName = "Base (English)", FileSize = 147_951_465, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin" },
        new() { FileName = "ggml-base.bin", DisplayName = "Base (Multilingual)", FileSize = 147_964_211, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" },
        new() { FileName = "ggml-small.en.bin", DisplayName = "Small (English)", FileSize = 487_601_967, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.en.bin" },
        new() { FileName = "ggml-small.bin", DisplayName = "Small (Multilingual)", FileSize = 487_626_547, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin" },
        new() { FileName = "ggml-medium.en.bin", DisplayName = "Medium (English)", FileSize = 1_533_774_781, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.en.bin" },
        new() { FileName = "ggml-medium.bin", DisplayName = "Medium (Multilingual)", FileSize = 1_533_774_781, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin" },
        new() { FileName = "ggml-large-v3-turbo.bin", DisplayName = "Large V3 Turbo (Best)", FileSize = 1_622_086_091, DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin" },
    ];
}
