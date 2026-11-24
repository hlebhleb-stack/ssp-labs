using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace OllamaClient;

public class OllamaApiClient
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly string _baseUrl;

    public OllamaApiClient(string baseUrl = "http://localhost:11434") => _baseUrl = baseUrl;

    // Список установленных моделей: GET /api/tags
    public async Task<List<string>> ListModelsAsync()
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("models").EnumerateArray()
            .Select(m => m.GetProperty("name").GetString()!).ToList();
    }

    // Обычная генерация: POST /api/generate, stream = false
    public async Task<string> GenerateAsync(string model, string prompt, string? systemPrompt = null,
        CancellationToken ct = default, int? maxTokens = null)
    {
        var options = new Dictionary<string, object> { ["temperature"] = 0.7 };
        if (maxTokens is int limit)
            options["num_predict"] = limit;

        var request = new { model, prompt, system = systemPrompt, stream = false, options };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.GetProperty("response").GetString() ?? "";
    }

    // Потоковая генерация: POST /api/generate, stream = true
    public async IAsyncEnumerable<string> GenerateStreamAsync(string model, string prompt,
        string? systemPrompt = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new { model, prompt, system = systemPrompt, stream = true, options = new { temperature = 0.7 } };
        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
        {
            Content = content
        };
        using var response = await _httpClient.SendAsync(
            message, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var document = JsonDocument.Parse(line);
            var token = document.RootElement.GetProperty("response").GetString();
            var done = document.RootElement.GetProperty("done").GetBoolean();
            if (!string.IsNullOrEmpty(token)) yield return token;
            if (done) break;
        }
    }
}
