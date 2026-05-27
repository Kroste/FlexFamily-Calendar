namespace FlexFamilyCalendar.Services.AI;

public class OpenAiProvider : IAiProvider
{
    private string? _apiKey;

    public string Name => "ChatGPT";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public void SetApiKey(string key) => _apiKey = key;

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => throw new NotImplementedException("ChatGPT-Integration noch nicht implementiert");
}
