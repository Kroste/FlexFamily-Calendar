using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlexFamilyCalendar.Api.Ai;

/// <summary>Server-seitiger Proxy für Cloud-AI-Provider. Hält die HTTP-Aufrufe an Anthropic/OpenAI/Gemini/Perplexity
/// hinter einer schmalen Fassade — gleiches Schema wie die Client-Provider-Klassen, nur mit ENV-Keys.</summary>
public class AiSender
{
    private readonly HttpClient _http;
    private readonly AiOptions _opts;
    private readonly ILogger<AiSender> _log;

    public AiSender(HttpClient http, AiOptions opts, ILogger<AiSender> log)
    {
        _http = http;
        _opts = opts;
        _log = log;
    }

    public async Task<(bool Ok, string Text, string? Error)> CompleteAsync(string provider, string prompt, string? model, CancellationToken ct)
    {
        try
        {
            return (provider ?? "").Trim().ToLowerInvariant() switch
            {
                "anthropic" => await CompleteAnthropicAsync(prompt, model, ct),
                "chatgpt" or "openai" => await CompleteOpenAiAsync(prompt, model, ct),
                "gemini" => await CompleteGeminiAsync(prompt, model, ct),
                "perplexity" => await CompletePerplexityAsync(prompt, model, ct),
                _ => (false, "", $"Unbekannter Provider: {provider}")
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AI-Provider {Provider} fehlgeschlagen", provider);
            return (false, "", ex.Message);
        }
    }

    private async Task<(bool, string, string?)> CompleteAnthropicAsync(string prompt, string? model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.AnthropicKey)) return (false, "", "Anthropic-Schlüssel serverseitig nicht gesetzt (Ai__Anthropic__Key).");
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _opts.AnthropicKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonContent.Create(new
        {
            model = string.IsNullOrWhiteSpace(model) ? "claude-haiku-4-5-20251001" : model,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = prompt } }
        });
        return await SendAndExtractAsync(req, ct, doc => doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "");
    }

    private async Task<(bool, string, string?)> CompleteOpenAiAsync(string prompt, string? model, CancellationToken ct)
        => await CompleteOpenAiCompatibleAsync("https://api.openai.com/v1/chat/completions", _opts.OpenAiKey,
            string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model, prompt, "OpenAI", "Ai__OpenAi__Key", ct);

    private async Task<(bool, string, string?)> CompletePerplexityAsync(string prompt, string? model, CancellationToken ct)
        => await CompleteOpenAiCompatibleAsync("https://api.perplexity.ai/chat/completions", _opts.PerplexityKey,
            string.IsNullOrWhiteSpace(model) ? "sonar" : model, prompt, "Perplexity", "Ai__Perplexity__Key", ct);

    private async Task<(bool, string, string?)> CompleteOpenAiCompatibleAsync(
        string endpoint, string key, string model, string prompt, string label, string envName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) return (false, "", $"{label}-Schlüssel serverseitig nicht gesetzt ({envName}).");
        var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        req.Content = JsonContent.Create(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } }
        });
        return await SendAndExtractAsync(req, ct, doc => doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "");
    }

    private async Task<(bool, string, string?)> CompleteGeminiAsync(string prompt, string? model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.GeminiKey)) return (false, "", "Gemini-Schlüssel serverseitig nicht gesetzt (Ai__Gemini__Key).");
        var m = string.IsNullOrWhiteSpace(model) ? "gemini-1.5-flash" : model;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent?key={_opts.GeminiKey}";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonContent.Create(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        });
        return await SendAndExtractAsync(req, ct, doc =>
            doc.RootElement.GetProperty("candidates")[0]
                .GetProperty("content").GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? "");
    }

    private async Task<(bool, string, string?)> SendAndExtractAsync(HttpRequestMessage req, CancellationToken ct, Func<JsonDocument, string> extract)
    {
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var snippet = body.Length > 500 ? body[..500] + "…" : body;
            return (false, "", $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} — {snippet}");
        }
        using var doc = JsonDocument.Parse(body);
        return (true, extract(doc), null);
    }
}
