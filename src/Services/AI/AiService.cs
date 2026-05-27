namespace FlexFamilyCalendar.Services.AI;

public class AiService
{
    private readonly Dictionary<string, IAiProvider> _providers;
    private string _activeProvider = "";

    public AiService(IEnumerable<IAiProvider> providers)
        => _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableProviders => _providers.Keys.Order().ToList();

    public IAiProvider? ActiveProvider
        => _providers.GetValueOrDefault(_activeProvider);

    public void SetActiveProvider(string name)
    {
        _activeProvider = name;
        LogService.Info("AI-Provider gewechselt zu: {0}", name);
    }

    public async Task<string?> SuggestAsync(string prompt, CancellationToken ct = default)
    {
        var provider = ActiveProvider;
        if (provider == null)
        {
            LogService.Warn("Kein AI-Provider ausgewählt");
            return null;
        }
        if (!provider.IsConfigured)
        {
            LogService.Warn("AI-Provider '{0}' hat keinen API-Key", provider.Name);
            return null;
        }
        LogService.Debug("AI-Anfrage an '{0}' ({1} Zeichen Prompt)", provider.Name, prompt.Length);
        try
        {
            var result = await provider.CompleteAsync(prompt, ct);
            LogService.Debug("AI-Antwort erhalten: {0} Zeichen", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            LogService.Error($"AI-Fehler bei Provider '{provider.Name}'", ex);
            return null;
        }
    }
}
