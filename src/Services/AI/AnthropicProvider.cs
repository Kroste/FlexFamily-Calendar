namespace FlexFamilyCalendar.Services.AI;

public class AnthropicProvider : HttpAiProvider
{
    public AnthropicProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "Anthropic";
    protected override string DefaultModel => "claude-3-5-haiku-latest";

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
