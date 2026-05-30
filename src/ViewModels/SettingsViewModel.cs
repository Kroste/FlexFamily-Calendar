using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public delegate Task UpdateCheckRunner(bool force);

public record GermanStateOption(GermanState State, string Name);

/// <summary>App-weite Einstellungen (Admin): Bundesland für Feiertage + pauschale Übernachtungs-Gutschrift.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly IMailSender _mailSender;
    private readonly UpdateCheckRunner? _runUpdateCheck;
    private AppSettings _settings = new();

    /// <summary>SMTP-Sektion nur anzeigen, wenn der Mail-Versand clientseitig läuft (Local-Modus).
    /// Im Server-/Browser-Modus liegt die SMTP-Konfig in ENV (Smtp__Host etc.) — UI würde nur verwirren.</summary>
    public bool ShowSmtpSection => !_mailSender.IsServerConfigured;

    /// <summary>Update-Sektion nur im Desktop — der Browser-Head wird vom Server ausgerollt.</summary>
    public bool ShowUpdateSection => !OperatingSystem.IsBrowser();

    public IReadOnlyList<GermanStateOption> States { get; }

    [ObservableProperty] private GermanStateOption? _selectedState;
    [ObservableProperty] private string _overnightHours = "2";
    [ObservableProperty] private string _smtpHost = "";
    [ObservableProperty] private string _smtpPort = "587";
    [ObservableProperty] private string _smtpUser = "";
    [ObservableProperty] private string _smtpFrom = "";
    [ObservableProperty] private bool _smtpUseSsl = true;
    [ObservableProperty] private string _smtpPassword = "";
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private bool _updateCheckEnabled = true;
    [ObservableProperty] private string _updateIntervalHours = "24";
    [ObservableProperty] private string _updateLastCheckedLabel = "";

    public SettingsViewModel(IStorageService storage, IMailSender mailSender, UpdateCheckRunner? runUpdateCheck = null)
    {
        _storage = storage;
        _mailSender = mailSender;
        _runUpdateCheck = runUpdateCheck;
        States = Enum.GetValues<GermanState>()
            .Select(s => new GermanStateOption(s, GermanStates.Names[s]))
            .OrderBy(o => o.Name)
            .ToList();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _settings = await _storage.LoadSettingsAsync();
        var current = GermanStates.Parse(_settings.HolidayState);
        SelectedState = States.FirstOrDefault(o => o.State == current) ?? States[0];
        OvernightHours = _settings.OvernightHoursPerDay.ToString(CultureInfo.CurrentCulture);
        SmtpHost = _settings.SmtpHost;
        SmtpPort = _settings.SmtpPort.ToString(CultureInfo.InvariantCulture);
        SmtpUser = _settings.SmtpUser;
        SmtpFrom = _settings.SmtpFrom;
        SmtpUseSsl = _settings.SmtpUseSsl;

        UpdateCheckEnabled = _settings.UpdateCheckEnabled;
        UpdateIntervalHours = _settings.UpdateCheckIntervalHours.ToString(CultureInfo.InvariantCulture);
        RefreshUpdateLastCheckedLabel();
    }

    private void RefreshUpdateLastCheckedLabel()
    {
        var when = _settings.UpdateLastCheckedAtUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
                   ?? Localizer.Instance["Settings_UpdateNever"];
        UpdateLastCheckedLabel = string.Format(Localizer.Instance["Settings_UpdateLastChecked"], when);
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (_runUpdateCheck is null) return;
        await _runUpdateCheck(force: true);
        // settings haben sich evtl. geändert (LastCheckedAt) — reloaden für das Label
        _settings = await _storage.LoadSettingsAsync();
        RefreshUpdateLastCheckedLabel();
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedState != null)
            _settings.HolidayState = SelectedState.State.ToString();
        if (double.TryParse(OvernightHours, NumberStyles.Any, CultureInfo.CurrentCulture, out var o) && o >= 0)
            _settings.OvernightHoursPerDay = o;

        _settings.SmtpHost = SmtpHost.Trim();
        _settings.SmtpUser = SmtpUser.Trim();
        _settings.SmtpFrom = SmtpFrom.Trim();
        _settings.SmtpUseSsl = SmtpUseSsl;
        if (int.TryParse(SmtpPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port) && port > 0)
            _settings.SmtpPort = port;
        if (!string.IsNullOrWhiteSpace(SmtpPassword))            // leer = bestehendes Passwort behalten
        {
            // SecretService.Initialize läuft nur im Desktop (Disk-Keyfile). Im Browser nimmt
            // localStorage den Klartext — Origin-Isolation ist dort die Schutzgrenze.
            _settings.SmtpPasswordEnc = SecretService.IsAvailable
                ? SecretService.Encrypt(SmtpPassword.Trim())
                : SmtpPassword.Trim();
        }

        _settings.UpdateCheckEnabled = UpdateCheckEnabled;
        if (int.TryParse(UpdateIntervalHours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) && h >= 0)
            _settings.UpdateCheckIntervalHours = h;

        await _storage.SaveSettingsAsync(_settings);
        SmtpPassword = "";
        StatusMessage = Localizer.Instance["Settings_Saved"];
        LogService.UserAction("Admin", "Einstellungen gespeichert");
    }
}
