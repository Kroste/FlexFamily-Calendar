namespace FlexFamilyCalendar.Services.AI;

public class GeminiProvider : HttpAiProvider
{
    public GeminiProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "Gemini";
    protected override string DefaultModel => "gemini-1.5-flash";

    public override async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{EffectiveModel}:generateContent?key={ApiKey}";
        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = JsonBody(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } }
        });
        var json = await SendAsync(req, ct);
        return json?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString() ?? "";
    }
}
