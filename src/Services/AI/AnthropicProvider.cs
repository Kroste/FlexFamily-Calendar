namespace FlexFamilyCalendar.Services.AI;

public class AnthropicProvider : HttpAiProvider
{
    public AnthropicProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "Anthropic";
    // Anthropic hat die 3.5-Generation abgekündigt; -latest-Aliasse antworten mit 404.
    // Aktueller Stand 2026: Haiku 4.5 (schnellstes/günstigstes Cloud-Modell).
    protected override string DefaultModel => "claude-haiku-4-5-20251001";

    public override async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", ApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = JsonBody(new
        {
            model = EffectiveModel,
            max_tokens = 1024,
            messages = new[] { new { role = "user", content = prompt } }
        });
        var json = await SendAsync(req, ct);
        return json["content"]?[0]?["text"]?.ToString() ?? "";
    }
}
