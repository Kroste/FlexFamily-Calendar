using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.Theming;

namespace FlexFamilyCalendar.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly IStorageService _storage;
    private readonly NotificationService _notifications;
    private readonly AiService _ai;
    private readonly IMailSender _mailSender;
    private User? _currentUser;

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

    // Plattform-Schalter: Window-basierte Dialoge/Features werden im Browser ausgeblendet, bis Inline-Ersatz da ist.
    public bool IsBrowser => OperatingSystem.IsBrowser();
    public bool IsNotBrowser => !OperatingSystem.IsBrowser();
    public bool IsAdminAndNotBrowser => IsAdmin && IsNotBrowser;

    public bool HasUnread => UnreadCount > 0;
    public string UnreadBadge => UnreadCount > 9 ? "9+" : UnreadCount.ToString();
    public LoginViewModel LoginVm { get; }

    /// <summary>Vom MainWindow-Code-Behind abonniert, um die jeweiligen Dialoge zu öffnen.</summary>
    public event Action? ProfileRequested;
    public event Action? MonthOverviewRequested;
    public event Action? HoursAccountRequested;
    public event Action? NotificationsRequested;
    public event Action? AdminRequested;

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
        OnPropertyChanged(nameof(IsAdminAndNotBrowser));
        LogService.UserAction(user.Username, logVerb);
        _ = RefreshUnreadCountAsync();
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
        CalendarVm?.Cleanup();
        IsLoggedIn = false;
        CalendarVm = null;
        CurrentUserDisplay = "";
        _currentUser = null;
        UnreadCount = 0;
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAdminAndNotBrowser));
        LoginVm.Username = "";
        LoginVm.RememberMe = false;
    }

    [RelayCommand]
    private void OpenNotifications() => NotificationsRequested?.Invoke();

    public NotificationsViewModel CreateNotifications() => new(_notifications, _currentUser!);

    [RelayCommand]
    private void OpenAdmin() => AdminRequested?.Invoke();

    public AdminViewModel CreateAdmin() => new(_auth, _storage, _ai);

    [RelayCommand]
    private void OpenProfile() => ProfileRequested?.Invoke();

    [RelayCommand]
    private void OpenMonthOverview() => MonthOverviewRequested?.Invoke();

    [RelayCommand]
    private void OpenHoursAccount() => HoursAccountRequested?.Invoke();

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
        OnPropertyChanged(nameof(IsAdminAndNotBrowser));
    }
}
