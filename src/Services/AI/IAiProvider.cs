namespace FlexFamilyCalendar.Services.AI;

public interface IAiProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    void SetApiKey(string key);
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
