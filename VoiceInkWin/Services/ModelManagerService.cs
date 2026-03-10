using System.IO;
using System.Net.Http;
using VoiceInkWin.Models;

namespace VoiceInkWin.Services;

public class ModelManagerService
{
    private readonly HttpClient _httpClient = new();
    private readonly string _modelsFolder;

    public event Action<double>? DownloadProgressChanged; // 0.0 to 1.0
    public event Action<string>? StatusChanged;

    public ModelManagerService()
    {
        _modelsFolder = SettingsService.GetModelsFolder();
        Directory.CreateDirectory(_modelsFolder);
    }

    public List<ModelInfo> GetAvailableModels()
    {
        var models = ModelInfo.GetAvailableModels();
        foreach (var model in models)
        {
            model.IsDownloaded = File.Exists(Path.Combine(_modelsFolder, model.FileName));
        }
        return models;
    }

    public List<ModelInfo> GetDownloadedModels()
    {
        return GetAvailableModels().Where(m => m.IsDownloaded).ToList();
    }

    public string? GetModelPath(string fileName)
    {
        var path = Path.Combine(_modelsFolder, fileName);
        return File.Exists(path) ? path : null;
    }

    public async Task DownloadModelAsync(ModelInfo model, CancellationToken cancellationToken = default)
    {
        var destPath = Path.Combine(_modelsFolder, model.FileName);
        var tempPath = destPath + ".tmp";

        StatusChanged?.Invoke($"Downloading {model.DisplayName}...");

        try
        {
            using var response = await _httpClient.GetAsync(model.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? model.FileSize;
            long bytesRead = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920);

            var buffer = new byte[81920];
            int read;
            while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                bytesRead += read;
                DownloadProgressChanged?.Invoke((double)bytesRead / totalBytes);
            }

            fileStream.Close();

            // Move temp to final
            if (File.Exists(destPath))
                File.Delete(destPath);
            File.Move(tempPath, destPath);

            model.IsDownloaded = true;
            StatusChanged?.Invoke($"{model.DisplayName} downloaded");
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            StatusChanged?.Invoke("Download cancelled");
            throw;
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            StatusChanged?.Invoke("Download failed");
            throw;
        }
    }

    public void DeleteModel(string fileName)
    {
        var path = Path.Combine(_modelsFolder, fileName);
        if (File.Exists(path))
            File.Delete(path);
    }
}
