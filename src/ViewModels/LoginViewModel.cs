using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly AuthService _auth;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isFirstRun;
    [ObservableProperty] private bool _rememberMe;

    public event Action<User>? LoginSuccessful;

    public LoginViewModel(AuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Bitte Benutzername und Kennwort eingeben.";
            return;
        }
        LogService.Click(Username, "Anmelden-Button");
        IsLoading = true;
        ErrorMessage = "";
        try
        {
            var user = await _auth.LoginAsync(Username, Password);
            if (user != null)
            {
                await _auth.SetRememberedUsernameAsync(RememberMe ? user.Username : null);
                LoginSuccessful?.Invoke(user);
            }
            else
                ErrorMessage = "Ungültiger Benutzername oder Kennwort.";
        }
        finally
        {
            IsLoading = false;
            Password = "";  // sofort aus dem Speicher entfernen
        }
    }

    [RelayCommand]
    private async Task CreateAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Bitte Benutzername und Kennwort eingeben.";
            return;
        }
        LogService.Click(Username, "Admin anlegen-Button");
        IsLoading = true;
        ErrorMessage = "";
        try
        {
            await _auth.CreateUserAsync(Username, Password,
                string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName, UserRole.Admin);
            IsFirstRun = false;
            var user = await _auth.LoginAsync(Username, Password);
            if (user != null)
            {
                await _auth.SetRememberedUsernameAsync(RememberMe ? user.Username : null);
                LoginSuccessful?.Invoke(user);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler beim Anlegen des Admin-Kontos", ex);
            ErrorMessage = "Fehler beim Anlegen des Kontos.";
        }
        finally
        {
            IsLoading = false;
            Password = "";
        }
    }
}
