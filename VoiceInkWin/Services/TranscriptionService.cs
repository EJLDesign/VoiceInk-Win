using Whisper.net;

using static VoiceInkWin.App;

namespace VoiceInkWin.Services;

public class TranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private string? _loadedModelPath;

    public event Action<string>? StatusChanged;

    public bool IsModelLoaded => _factory != null;

    public void LoadModel(string modelPath)
    {
        if (_loadedModelPath == modelPath && _factory != null)
            return;

        _factory?.Dispose();
        StatusChanged?.Invoke("Loading model...");

        _factory = WhisperFactory.FromPath(modelPath);
        _loadedModelPath = modelPath;

        StatusChanged?.Invoke("Model loaded");
    }

    public async Task<string> TranscribeAsync(float[] audioData, string language = "en",
        bool translate = false, string? prompt = null)
    {
        if (_factory == null)
            throw new InvalidOperationException("No model loaded. Please download and select a model.");

        StatusChanged?.Invoke("Transcribing...");

        var builder = _factory.CreateBuilder()
            .WithLanguage(language)
            .WithPrintTimestamps(false);

        if (translate)
            builder.WithTranslate();

        if (!string.IsNullOrWhiteSpace(prompt))
            builder.WithPrompt(prompt);

        using var processor = builder.Build();

        var segments = new List<string>();
        int segIndex = 0;
        await foreach (var segment in processor.ProcessAsync(audioData))
        {
            var text = segment.Text.Trim();
            App.Log($"Whisper segment[{segIndex}]: \"{text}\"");
            segIndex++;
            // Filter blank/noise segments
            if (!string.IsNullOrWhiteSpace(text) &&
                !text.Equals("[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith('['))
            {
                segments.Add(text);
            }
        }

        var result = string.Join(" ", segments).Trim();

        // Detect Whisper hallucination (same word repeated, e.g. "you you you")
        var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && words.All(w => w.Equals(words[0], StringComparison.OrdinalIgnoreCase)))
        {
            App.Log($"Hallucination detected: \"{result}\"");
            StatusChanged?.Invoke("No speech detected");
            return "";
        }

        StatusChanged?.Invoke(string.IsNullOrEmpty(result) ? "No speech detected" : "Done");
        return result;
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
