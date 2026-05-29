using System.Net.Http.Json;
using FlexFamilyCalendar.Services.AI;
using Newtonsoft.Json.Linq;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Browser-/Server-Modus: ruft <c>/api/ai/complete</c>. Schlüssel liegen serverseitig
/// in ENV (Ai__&lt;Provider&gt;__Key) — der Client trägt nur den Provider-Namen und den Prompt.</summary>
public class ApiAiProvider : IAiProvider
{
    private readonly ApiClient _api;
    private string _model = "";

    public ApiAiProvider(ApiClient api, string name)
    {
        _api = api;
        Name = name;
    }

    public string Name { get; }

    /// <summary>Serverseitiger Provider — kein clientseitiger Schlüssel nötig.</summary>
    public bool RequiresApiKey => false;
    public bool IsServerConfigured => true;   // Key liegt serverseitig in ENV
    public bool IsConfigured => true;   // Server entscheidet beim Call
    public void SetApiKey(string key) { /* serverseitig */ }
    public void SetModel(string model) => _model = model ?? "";

    public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => await _api.AiCompleteAsync(Name, prompt, string.IsNullOrWhiteSpace(_model) ? null : _model, ct);
}
