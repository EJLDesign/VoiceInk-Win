using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VoiceInkWin.Models;

namespace VoiceInkWin.Services;

public class AIPostProcessorService
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly CredentialService _credentialService;
    private readonly SettingsService _settingsService;

    public AIPostProcessorService(CredentialService credentialService, SettingsService settingsService)
    {
        _credentialService = credentialService;
        _settingsService = settingsService;
    }

    public async Task<string> ProcessAsync(string rawText, Mode mode)
    {
        if (string.IsNullOrWhiteSpace(rawText) || mode.Name == "Raw" || string.IsNullOrEmpty(mode.SystemPrompt))
            return rawText;

        var provider = _settingsService.Settings.AiProvider;

        try
        {
            return provider switch
            {
                "anthropic" => await ProcessWithAnthropicAsync(rawText, mode.SystemPrompt),
                "ollama" => await ProcessWithOllamaAsync(rawText, mode.SystemPrompt),
                _ => rawText
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AI post-processing failed: {ex.Message}");
            return rawText; // Silent fallback
        }
    }

    private async Task<string> ProcessWithAnthropicAsync(string text, string systemPrompt)
    {
        var apiKey = _credentialService.GetApiKey("anthropic");
        if (string.IsNullOrEmpty(apiKey))
            return text;

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var body = new
        {
            model = "claude-sonnet-4-5-20250929",
            max_tokens = 1024,
            temperature = 0,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = text } }
        };

        request.Content = JsonContent.Create(body);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("content")[0].GetProperty("text").GetString() ?? text;
    }

    private async Task<string> ProcessWithOllamaAsync(string text, string systemPrompt)
    {
        var settings = _settingsService.Settings;
        var endpoint = settings.OllamaEndpoint.TrimEnd('/');

        var body = new
        {
            model = settings.OllamaModelName,
            prompt = text,
            system = systemPrompt,
            stream = false
        };

        var response = await _httpClient.PostAsJsonAsync($"{endpoint}/api/generate", body);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("response").GetString()?.Trim() ?? text;
    }
}
