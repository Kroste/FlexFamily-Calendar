namespace FlexFamilyCalendar.Services.AI;

public class PerplexityProvider : IAiProvider
{
    private string? _apiKey;

    public string Name => "Perplexity";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public void SetApiKey(string key) => _apiKey = key;

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => throw new NotImplementedException("Perplexity-Integration noch nicht implementiert");
}
