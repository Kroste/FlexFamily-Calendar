namespace FlexFamilyCalendar.Services.AI;

public interface IAiProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    bool RequiresApiKey { get; }
    void SetApiKey(string key);
    void SetModel(string model);
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
