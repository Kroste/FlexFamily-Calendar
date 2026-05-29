namespace FlexFamilyCalendar.Services.AI;

public interface IAiProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    bool RequiresApiKey { get; }

    /// <summary>True = Schlüssel/Endpoint liegen serverseitig (ENV); die Settings-UI zeigt
    /// dann weder Key- noch Endpoint-Feld an, sondern nur einen Hinweis.</summary>
    bool IsServerConfigured { get; }

    void SetApiKey(string key);
    void SetModel(string model);
    Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
}
