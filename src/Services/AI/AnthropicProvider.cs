namespace FlexFamilyCalendar.Services.AI;

public class AnthropicProvider : IAiProvider
{
    private string? _apiKey;

    public string Name => "Anthropic";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public void SetApiKey(string key) => _apiKey = key;

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => throw new NotImplementedException("Anthropic-Integration noch nicht implementiert");
}
