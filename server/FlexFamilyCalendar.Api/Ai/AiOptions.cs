namespace FlexFamilyCalendar.Api.Ai;

/// <summary>API-Schlüssel der unterstützten Cloud-Provider aus ENV (Ai__Anthropic__Key etc.,
/// 12-Factor-Style wie Jwt__*/Smtp__*). Leer = Provider serverseitig nicht konfiguriert.</summary>
public class AiOptions
{
    public string AnthropicKey { get; set; } = "";
    public string OpenAiKey { get; set; } = "";
    public string GeminiKey { get; set; } = "";
    public string PerplexityKey { get; set; } = "";

    public bool HasKey(string provider) => (provider ?? "").Trim().ToLowerInvariant() switch
    {
        "anthropic" => !string.IsNullOrWhiteSpace(AnthropicKey),
        "chatgpt" or "openai" => !string.IsNullOrWhiteSpace(OpenAiKey),
        "gemini" => !string.IsNullOrWhiteSpace(GeminiKey),
        "perplexity" => !string.IsNullOrWhiteSpace(PerplexityKey),
        _ => false
    };
}
