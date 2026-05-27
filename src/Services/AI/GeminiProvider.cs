namespace FlexFamilyCalendar.Services.AI;

public class GeminiProvider : IAiProvider
{
    private string? _apiKey;

    public string Name => "Gemini";
    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);
    public void SetApiKey(string key) => _apiKey = key;

    public Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        => throw new NotImplementedException("Gemini-Integration noch nicht implementiert");
}
