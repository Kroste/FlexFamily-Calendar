using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.AI;

public class AiService
{
    private readonly Dictionary<string, IAiProvider> _providers;
    private string _activeProvider = "";

    public AiService(IEnumerable<IAiProvider> providers)
        => _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> AvailableProviders => _providers.Keys.Order().ToList();

    public IAiProvider? GetProvider(string name) => _providers.GetValueOrDefault(name);

    public IAiProvider? ActiveProvider
        => _providers.GetValueOrDefault(_activeProvider);

    public void SetActiveProvider(string name)
    {
        _activeProvider = name;
        LogService.Info("AI-Provider gewechselt zu: {0}", name);
    }

    /// <summary>Entschlüsselt die gespeicherten Schlüssel und wendet Provider-Auswahl + Modell an.</summary>
    public void ApplySettings(AppSettings settings)
    {
        foreach (var (name, encrypted) in settings.EncryptedApiKeys)
        {
            if (!_providers.TryGetValue(name, out var provider) || string.IsNullOrEmpty(encrypted)) continue;
            try { provider.SetApiKey(SecretService.Decrypt(encrypted)); }
            catch (Exception ex) { LogService.Error($"AI-Schlüssel für '{name}' konnte nicht entschlüsselt werden", ex); }
        }
        if (!string.IsNullOrEmpty(settings.ActiveAiProvider))
            _activeProvider = settings.ActiveAiProvider;
        if (!string.IsNullOrWhiteSpace(settings.AiModel))
            ActiveProvider?.SetModel(settings.AiModel);
    }

    /// <summary>Kurzer Verbindungstest über den aktiven Provider; true bei nicht-leerer Antwort.</summary>
    public async Task<bool> TestAsync(CancellationToken ct = default)
    {
        var result = await SuggestAsync("Antworte ausschließlich mit: OK", ct);
        return !string.IsNullOrWhiteSpace(result);
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
