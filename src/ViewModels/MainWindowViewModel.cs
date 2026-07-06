using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.Services.Update;
using FlexFamilyCalendar.Theming;

namespace FlexFamilyCalendar.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string UpdateRepoOwner = "Kroste";
    private const string UpdateRepoName = "FlexFamily-Calendar";

    private readonly AuthService _auth;
    private readonly IStorageService _storage;
    private readonly NotificationService _notifications;
    private readonly AiService _ai;
    private readonly IMailSender _mailSender;
    private readonly UpdateService _updateService = new(UpdateRepoOwner, UpdateRepoName);
    private bool _updateCheckRunning;
    private User? _currentUser;

    // Cross-Client-Sync: alle SyncIntervalSeconds Sekunden frische Daten ziehen,
    // damit Änderungen aus anderen Sessions (App ↔ Web, andere Familienmitglieder)
    // ohne manuelles Reload sichtbar werden. Klein genug für 5–10 Nutzer.
    private const double SyncIntervalSeconds = 30;
    private Avalonia.Threading.DispatcherTimer? _syncTimer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    private bool _isLoggedIn;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _currentUserDisplay = "";
    [ObservableProperty] private CalendarViewModel? _calendarVm;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnread))]
    [NotifyPropertyChangedFor(nameof(UnreadBadge))]
    private int _unreadCount;

    public bool IsNotLoggedIn => !IsLoggedIn;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;

    public bool HasUnread => UnreadCount > 0;
    public string UnreadBadge => UnreadCount > 9 ? "9+" : UnreadCount.ToString();
    public LoginViewModel LoginVm { get; }

    /// <summary>Vom MainWindow-Code-Behind abonniert, um die jeweiligen Dialoge zu öffnen.</summary>
    public event Action? ProfileRequested;
    public event Action? MonthOverviewRequested;
    public event Action? HoursAccountRequested;
    public event Action? NotificationsRequested;
    public event Action? AdminRequested;
    public event Action? InfoRequested;

    public MainWindowViewModel(AuthService auth, IStorageService storage, NotificationService notifications, AiService ai, IMailSender mailSender, LoginViewModel loginVm)
    {
        _auth = auth;
        _storage = storage;
        _notifications = notifications;
        _ai = ai;
        _mailSender = mailSender;
        LoginVm = loginVm;
        LoginVm.LoginSuccessful += OnLoginSuccessful;
        LogService.StatusUpdated += msg =>
            Avalonia.Threading.Dispatcher.UIThread.Post(() => StatusMessage = msg);
        LogService.Info("FlexFamily Calendar gestartet");
    }

    private void OnLoginSuccessful(User user) => CompleteLogin(user, "Angemeldet");

    /// <summary>Auto-Login beim Start mit gemerktem Benutzer (kein Passwort nötig).</summary>
    public void AutoLogin(User user) => CompleteLogin(user, "Automatisch angemeldet");

    private void CompleteLogin(User user, string logVerb)
    {
        _currentUser = user;
        Localizer.Instance.SetLanguage(user.Language);
        ThemeManager.Instance.Apply(user.ThemeVariant);

        CurrentUserDisplay = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName;
        CalendarVm?.Cleanup();
        CalendarVm = new CalendarViewModel(_storage, user, _notifications, _ai, _mailSender);
        IsLoggedIn = true;
        OnPropertyChanged(nameof(IsAdmin));
        LogService.UserAction(user.Username, logVerb);
        _ = RefreshUnreadCountAsync();
        StartBackgroundSync();
        // Update-Check NACH dem aktuellen UI-Tick. Im AutoLogin-Pfad wird App.DialogService
        // erst gesetzt, nachdem OnFrameworkInitializationCompleted aus dem Login zurückkehrt;
        // Background-Prio sorgt dafür, dass dieser Block danach läuft.
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => _ = CheckForUpdatesIfDueAsync(force: false),
            Avalonia.Threading.DispatcherPriority.Background);
    }

    private void StartBackgroundSync()
    {
        StopBackgroundSync();
        _syncTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(SyncIntervalSeconds)
        };
        _syncTimer.Tick += async (_, _) => await BackgroundSyncAsync();
        _syncTimer.Start();
    }

    private void StopBackgroundSync()
    {
        if (_syncTimer is null) return;
        _syncTimer.Stop();
        _syncTimer = null;
    }

    private async Task BackgroundSyncAsync()
    {
        if (!IsLoggedIn || CalendarVm is null) return;
        try
        {
            await CalendarVm.RefreshAllAsync(silent: true);
            await RefreshUnreadCountAsync();
        }
        catch (Exception ex)
        {
            // Sync-Fehler nicht in die Statusleiste — sonst flutet ein offline-Moment die UI.
            LogService.Debug("Hintergrund-Sync fehlgeschlagen: {0}", ex.Message);
        }
    }

    public async Task RefreshUnreadCountAsync()
    {
        UnreadCount = _currentUser == null ? 0 : await _notifications.UnreadCountAsync(_currentUser.Id);
    }

    [RelayCommand]
    private void Logout()
    {
        LogService.UserAction(CurrentUserDisplay, "Abgemeldet");
        _ = _auth.SetRememberedUsernameAsync(null);  // Auto-Login deaktivieren
        StopBackgroundSync();
        CalendarVm?.Cleanup();
        IsLoggedIn = false;
        CalendarVm = null;
        CurrentUserDisplay = "";
        _currentUser = null;
        UnreadCount = 0;
        OnPropertyChanged(nameof(IsAdmin));
        LoginVm.Username = "";
        LoginVm.RememberMe = false;
    }

    [RelayCommand]
    private void OpenNotifications() => NotificationsRequested?.Invoke();

    public NotificationsViewModel CreateNotifications() => new(_notifications, _currentUser!);

    [RelayCommand]
    private void OpenAdmin() => AdminRequested?.Invoke();

    public AdminViewModel CreateAdmin()
        => new(_auth, _storage, _ai, _mailSender, force => CheckForUpdatesIfDueAsync(force));

    /// <summary>
    /// Auto-Update-Kern: prüft Intervall + Enabled-Flag, holt das aktuellste Release vom GitHub-
    /// Repo, vergleicht Version, und öffnet den Dialog falls neuer. <paramref name="force"/>=true
    /// ignoriert das Intervall (Aufruf aus dem Settings-„Jetzt prüfen"-Button).
    /// </summary>
    private async Task CheckForUpdatesIfDueAsync(bool force)
    {
        if (_updateCheckRunning) return;
        if (OperatingSystem.IsBrowser()) return;     // Browser-Head: Update kommt über Watchtower
        if (UpdateService.DetectPlatform() == UpdatePlatform.Unsupported) return;

        try
        {
            _updateCheckRunning = true;
            var settings = await _storage.LoadSettingsAsync();
            if (!force)
            {
                if (!settings.UpdateCheckEnabled) return;
                if (settings.UpdateLastCheckedAtUtc is { } last)
                {
                    var dueAt = last.AddHours(Math.Max(1, settings.UpdateCheckIntervalHours));
                    if (DateTime.UtcNow < dueAt) return;
                }
            }

            LogService.Debug("Auto-Update: prüfe GitHub-Release (current={0}, force={1})",
                UpdateService.CurrentVersion(), force);
            var info = await _updateService.CheckAsync();

            if (info is null)
            {
                // Frage gestellt + Antwort „keine neuere Version" → Intervall starten.
                settings.UpdateLastCheckedAtUtc = DateTime.UtcNow;
                await _storage.SaveSettingsAsync(settings);
                LogService.Debug("Auto-Update: keine neuere Version gefunden.");
                return;
            }
            if (settings.UpdateSkippedVersions.Contains(info.LatestVersion))
            {
                settings.UpdateLastCheckedAtUtc = DateTime.UtcNow;
                await _storage.SaveSettingsAsync(settings);
                LogService.Debug("Auto-Update: Version {0} wurde übersprungen, kein Dialog.", info.LatestVersion);
                return;
            }

            // Auf DialogService warten — beim AutoLogin im Startup kommt er erst NACH dem
            // OnFrameworkInitializationCompleted-Block. Max 3 s, sonst bei nächstem Login retryen.
            for (int i = 0; i < 30 && App.DialogService is null; i++)
                await Task.Delay(100);

            if (App.DialogService is null)
            {
                LogService.Warn("Auto-Update: Dialog-Backend kam nicht rechtzeitig — LastChecked NICHT gesetzt, nächster Login retryt.");
                return;
            }

            // Erst HIER LastChecked setzen, damit ein Race nicht den Check für 24h begräbt.
            settings.UpdateLastCheckedAtUtc = DateTime.UtcNow;
            await _storage.SaveSettingsAsync(settings);

            var vm = new UpdateViewModel(info);
            var action = await App.DialogService.ShowUpdateAsync(vm);
            switch (action)
            {
                case UpdateDialogAction.Install:
                    await RunUpdateAsync(info);
                    break;
                case UpdateDialogAction.OpenReleasePage:
                    OpenBrowser(info.ReleaseUrl);
                    break;
                case UpdateDialogAction.Skip:
                    settings.UpdateSkippedVersions.Add(info.LatestVersion);
                    await _storage.SaveSettingsAsync(settings);
                    LogService.UserAction(_currentUser?.Username ?? "?", $"Update {info.LatestVersion} übersprungen");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Debug("Auto-Update fehlgeschlagen: {0}", ex.Message);
        }
        finally
        {
            _updateCheckRunning = false;
        }
    }

    private async Task RunUpdateAsync(UpdateInfo info)
    {
        if (info.Asset is null) { LogService.Warn("Auto-Update: kein passendes Asset im Release."); return; }
        var installer = UpdateInstallerFactory.ForPlatform(info.Asset.Platform);
        if (installer is null) { LogService.Warn("Auto-Update: keine Installer-Strategie für {0}.", info.Asset.Platform); return; }

        var dl = Path.Combine(Path.GetTempPath(), info.Asset.FileName);
        LogService.Info("Auto-Update: lade {0} → {1}", info.Asset.FileName, dl);
        await _updateService.DownloadAsync(info.Asset, dl);

        LogService.UserAction(_currentUser?.Username ?? "?", $"Update auf {info.LatestVersion} startet");
        await installer.InstallAndRestartAsync(dl, CancellationToken.None);
        // installer.* startet den Update-Helper im Hintergrund. Damit der gerade laufende
        // Prozess nicht die zu ersetzenden Dateien sperrt, beenden wir uns hier sofort.
        Environment.Exit(0);
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { LogService.Warn("Browser-Öffnen schlug fehl: {0}", ex.Message); }
    }

    [RelayCommand]
    private void OpenProfile() => ProfileRequested?.Invoke();

    [RelayCommand]
    private void OpenMonthOverview() => MonthOverviewRequested?.Invoke();

    [RelayCommand]
    private void OpenHoursAccount() => HoursAccountRequested?.Invoke();

    [RelayCommand]
    private void OpenInfo() => InfoRequested?.Invoke();

    public InfoViewModel CreateInfo() => new();

    public UserEditorViewModel CreateProfileEditor()
        => new(_auth, _currentUser, isNew: false, selfMode: true);

    public MonthOverviewViewModel CreateMonthOverview()
    {
        // gleiche Sicht-Regel wie das Wochen-Panel: Nicht-Admin nur er selbst;
        // Admin folgt seiner aktuellen Kalender-Ansicht (Planung = alle, Meine Sicht = nur er)
        var personalView = _currentUser is null
            || _currentUser.Role != UserRole.Admin
            || (CalendarVm?.IsPersonalView ?? false);
        return new MonthOverviewViewModel(_storage, _currentUser!, personalView);
    }

    public HoursAccountViewModel CreateHoursAccount()
        => new(_storage, _currentUser!, _currentUser?.Role == UserRole.Admin);

    /// <summary>Nach Profil-/Verwaltungs-Dialog: aktuellen Benutzer neu laden, Sprache/Anzeige anwenden.</summary>
    public async Task RefreshCurrentUserAsync()
    {
        if (_currentUser == null) return;
        var users = await _auth.GetUsersAsync();
        var fresh = users.FirstOrDefault(u => u.Id == _currentUser.Id);
        if (fresh == null) return;
        _currentUser = fresh;
        Localizer.Instance.SetLanguage(fresh.Language);
        ThemeManager.Instance.Apply(fresh.ThemeVariant);
        CurrentUserDisplay = string.IsNullOrEmpty(fresh.DisplayName) ? fresh.Username : fresh.DisplayName;
        OnPropertyChanged(nameof(IsAdmin));
    }
}
