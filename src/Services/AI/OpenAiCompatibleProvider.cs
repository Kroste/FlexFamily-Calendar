namespace FlexFamilyCalendar.Services.AI;

/// <summary>Basis für Provider mit OpenAI-kompatibler Chat-Completions-API (OpenAI, Perplexity).</summary>
public abstract class OpenAiCompatibleProvider : HttpAiProvider
{
    protected OpenAiCompatibleProvider(HttpClient? http) : base(http) { }

    protected abstract string Endpoint { get; }

    public override async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        req.Headers.Add("Authorization", $"Bearer {ApiKey}");
        req.Content = JsonBody(new
        {
            model = EffectiveModel,
            messages = new[] { new { role = "user", content = prompt } }
        });
        var json = await SendAsync(req, ct);
        return json["choices"]?[0]?["message"]?["content"]?.ToString() ?? "";
    }
}
