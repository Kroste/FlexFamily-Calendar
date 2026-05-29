using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;

namespace FlexFamilyCalendar.ViewModels;

public partial class AiSettingsViewModel : ViewModelBase
{
    private readonly AiService _ai;
    private readonly IStorageService _storage;
    private AppSettings _settings = new();

    public IReadOnlyList<string> Providers => _ai.AvailableProviders;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequiresKey))]
    [NotifyPropertyChangedFor(nameof(IsLocal))]
    [NotifyPropertyChangedFor(nameof(IsServerConfigured))]
    [NotifyPropertyChangedFor(nameof(ShowApiKey))]
    [NotifyPropertyChangedFor(nameof(ShowEndpoint))]
    [NotifyPropertyChangedFor(nameof(KeyPlaceholder))]
    private string? _selectedProvider;

    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _statusMessage = "";

    public bool RequiresKey => _ai.GetProvider(SelectedProvider ?? "")?.RequiresApiKey ?? true;
    public bool IsServerConfigured => _ai.GetProvider(SelectedProvider ?? "")?.IsServerConfigured ?? false;
    public bool IsLocal => !RequiresKey && !IsServerConfigured;

    /// <summary>Schlüssel-Feld nur, wenn der Provider den Schlüssel im Client erwartet.</summary>
    public bool ShowApiKey => RequiresKey && !IsServerConfigured;
    /// <summary>Endpoint-Feld nur für lokale Provider (z.B. Llama auf localhost).</summary>
    public bool ShowEndpoint => IsLocal;

    private bool HasStoredKey => SelectedProvider != null
        && _settings.EncryptedApiKeys.TryGetValue(SelectedProvider, out var v) && !string.IsNullOrEmpty(v);

    public string KeyPlaceholder => HasStoredKey ? Localizer.Instance["Ai_KeyStored"] : "";

    public event Action? Closed;

    public AiSettingsViewModel(AiService ai, IStorageService storage)
    {
        _ai = ai;
        _storage = storage;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _settings = await _storage.LoadSettingsAsync();
        Model = _settings.AiModel;
        SelectedProvider = string.IsNullOrEmpty(_settings.ActiveAiProvider)
            ? Providers.FirstOrDefault()
            : _settings.ActiveAiProvider;
    }

    /// <summary>Überträgt die aktuellen Eingaben in das Settings-Objekt (Schlüssel nur wenn neu eingegeben).</summary>
    private void ApplyToSettings()
    {
        if (SelectedProvider == null) return;
        _settings.ActiveAiProvider = SelectedProvider;
        _settings.AiModel = Model.Trim();
        if (!string.IsNullOrWhiteSpace(ApiKey))
        {
            // SecretService.Initialize läuft nur im Desktop (Disk-Keyfile). Im Browser nimmt
            // localStorage den Klartext — die Origin-Isolation ist dort die Schutzgrenze
            // (gleiches Muster wie AuthService.SetRememberedUsernameAsync mit dem JWT).
            _settings.EncryptedApiKeys[SelectedProvider] = SecretService.IsAvailable
                ? SecretService.Encrypt(ApiKey.Trim())
                : ApiKey.Trim();
        }
    }

    [RelayCommand]
    private async Task Test()
    {
        ApplyToSettings();
        _ai.ApplySettings(_settings);
        StatusMessage = Localizer.Instance["Ai_Testing"];
        var ok = await _ai.TestAsync();
        StatusMessage = Localizer.Instance[ok ? "Ai_TestOk" : "Ai_TestFailed"];
    }

    [RelayCommand]
    private async Task Save()
    {
        ApplyToSettings();
        await _storage.SaveSettingsAsync(_settings);
        _ai.ApplySettings(_settings);
        LogService.UserAction("Admin", $"KI-Provider gesetzt: {SelectedProvider}");
        Closed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();
}
