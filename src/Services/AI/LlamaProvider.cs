namespace FlexFamilyCalendar.Services.AI;

/// <summary>Local LLama via Ollama or llama.cpp HTTP API — auto-detected on localhost.</summary>
public class LlamaProvider : IAiProvider
{
    private string _endpoint = "http://localhost:11434";

    public string Name => "LLama (lokal)";
    public bool IsConfigured => true;  // Local — no API key needed
    public void SetApiKey(string key) => _endpoint = key;  // key = endpoint URL

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => throw new NotImplementedException("LLama-Integration noch nicht implementiert");
}
