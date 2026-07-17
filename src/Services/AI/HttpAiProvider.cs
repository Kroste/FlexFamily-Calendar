using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlexFamilyCalendar.Services.AI;

/// <summary>Gemeinsame HTTP-Mechanik der Cloud-Provider (Senden, Fehlerbehandlung, JSON-Parsen).</summary>
public abstract class HttpAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    protected string ApiKey = "";
    protected string Model = "";

    protected HttpAiProvider(HttpClient? http) => _http = http ?? new HttpClient();

    public abstract string Name { get; }
    protected abstract string DefaultModel { get; }

    public virtual bool RequiresApiKey => true;
    public virtual bool IsServerConfigured => false;
    public virtual bool IsConfigured => !string.IsNullOrEmpty(ApiKey);
    public virtual void SetApiKey(string key) => ApiKey = key ?? "";
    public void SetModel(string model) => Model = model ?? "";

    protected string EffectiveModel => string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model;

    public abstract Task<string> CompleteAsync(string prompt, CancellationToken ct = default);

    /// <summary>Sendet die Anfrage, prüft den Statuscode und gibt die geparste JSON-Antwort zurück.</summary>
    protected async Task<JsonNode?> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var resp = await _http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            // API-Body trägt fast immer den eigentlichen Grund (Anthropic/OpenAI/etc. liefern
            // JSON mit error.message). Nur die ersten 500 Zeichen mitgeben, damit Logs nicht
            // explodieren — reicht für Diagnose von Auth/Modell/Rate-Limit-Fehlern.
            var snippet = body.Length > 500 ? body[..500] + "…" : body;
            throw new HttpRequestException(
                $"{Name}: HTTP {(int)resp.StatusCode} {resp.ReasonPhrase} — {snippet}");
        }
        return JsonNode.Parse(body);
    }

    protected static StringContent JsonBody(object body)
        => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
}
