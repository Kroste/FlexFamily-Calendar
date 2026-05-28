using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
    public virtual bool IsConfigured => !string.IsNullOrEmpty(ApiKey);
    public virtual void SetApiKey(string key) => ApiKey = key ?? "";
    public void SetModel(string model) => Model = model ?? "";

    protected string EffectiveModel => string.IsNullOrWhiteSpace(Model) ? DefaultModel : Model;

    public abstract Task<string> CompleteAsync(string prompt, CancellationToken ct = default);

    /// <summary>Sendet die Anfrage, prüft den Statuscode und gibt die geparste JSON-Antwort zurück.</summary>
    protected async Task<JObject> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using var resp = await _http.SendAsync(request, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"{Name}: HTTP {(int)resp.StatusCode}");
        return JObject.Parse(body);
    }

    protected static StringContent JsonBody(object body)
        => new(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
}
