using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly StorageService _storage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    private bool _isLoggedIn;

    [ObservableProperty] private string _statusMessage = "Bereit";
    [ObservableProperty] private string _currentUserDisplay = "";
    [ObservableProperty] private CalendarViewModel? _calendarVm;

    public bool IsNotLoggedIn => !IsLoggedIn;
    public LoginViewModel LoginVm { get; }

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
        CurrentUserDisplay = string.IsNullOrEmpty(user.DisplayName) ? user.Username : user.DisplayName;
        CalendarVm = new CalendarViewModel(_storage, user);
        IsLoggedIn = true;
        LogService.UserAction(user.Username, logVerb);
    }

    [RelayCommand]
    private void Logout()
    {
        LogService.UserAction(CurrentUserDisplay, "Abgemeldet");
        _ = _auth.SetRememberedUsernameAsync(null);  // Auto-Login deaktivieren
        IsLoggedIn = false;
        CalendarVm = null;
        CurrentUserDisplay = "";
        LoginVm.Username = "";
        LoginVm.RememberMe = false;
    }
}
