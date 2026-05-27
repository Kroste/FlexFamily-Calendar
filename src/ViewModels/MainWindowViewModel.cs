using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Theming;

namespace FlexFamilyCalendar.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly StorageService _storage;
    private User? _currentUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    private bool _isLoggedIn;

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _currentUserDisplay = "";
    [ObservableProperty] private CalendarViewModel? _calendarVm;

    public bool IsNotLoggedIn => !IsLoggedIn;
    public bool IsAdmin => _currentUser?.Role == UserRole.Admin;
    public LoginViewModel LoginVm { get; }

    /// <summary>Vom MainWindow-Code-Behind abonniert, um die jeweiligen Dialoge zu öffnen.</summary>
    public event Action? ProfileRequested;
    public event Action? UserManagementRequested;

    public MainWindowViewModel(AuthService auth, StorageService storage, LoginViewModel loginVm)
    {
        _auth = auth;
        _storage = storage;
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
        CalendarVm = new CalendarViewModel(_storage, user);
        IsLoggedIn = true;
        OnPropertyChanged(nameof(IsAdmin));
        LogService.UserAction(user.Username, logVerb);
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
        OnPropertyChanged(nameof(IsAdmin));
        LoginVm.Username = "";
        LoginVm.RememberMe = false;
    }

    [RelayCommand]
    private void OpenProfile() => ProfileRequested?.Invoke();

    [RelayCommand]
    private void OpenUserManagement() => UserManagementRequested?.Invoke();

    public UserEditorViewModel CreateProfileEditor()
        => new(_auth, _currentUser, isNew: false, selfMode: true);

    public UserManagementViewModel CreateUserManagement()
        => new(_auth);

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
