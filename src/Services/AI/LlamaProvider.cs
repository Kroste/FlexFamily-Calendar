namespace FlexFamilyCalendar.Services.AI;

/// <summary>Lokale Llama via Ollama-HTTP-API auf localhost — kein API-Schlüssel nötig (freie Option).</summary>
public class LlamaProvider : HttpAiProvider
{
    private string _endpoint = "http://localhost:11434";

    public LlamaProvider(HttpClient? http = null) : base(http) { }

    public override string Name => "LLama (lokal)";
    protected override string DefaultModel => "llama3.2";

    public override bool RequiresApiKey => false;
    public override bool IsConfigured => true;
    // „Schlüssel" = Endpoint-URL (leer → Standard localhost)
    public override void SetApiKey(string key) => _endpoint = string.IsNullOrWhiteSpace(key) ? "http://localhost:11434" : key;

    public override async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_endpoint.TrimEnd('/')}/api/generate");
        req.Content = JsonBody(new { model = EffectiveModel, prompt, stream = false });
        var json = await SendAsync(req, ct);
        return json?["response"]?.ToString() ?? "";
    }
}
