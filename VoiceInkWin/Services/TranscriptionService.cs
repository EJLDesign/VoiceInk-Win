using Whisper.net;

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
        await foreach (var segment in processor.ProcessAsync(audioData))
        {
            var text = segment.Text.Trim();
            // Filter blank/noise segments
            if (!string.IsNullOrWhiteSpace(text) &&
                !text.Equals("[BLANK_AUDIO]", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith('['))
            {
                segments.Add(text);
            }
        }

        var result = string.Join(" ", segments).Trim();
        StatusChanged?.Invoke(string.IsNullOrEmpty(result) ? "No speech detected" : "Done");
        return result;
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
