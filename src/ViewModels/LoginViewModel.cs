using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
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
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private string _connectionLabel = "";

    public IReadOnlyList<LanguageOption> AvailableLanguages => Localizer.Instance.AvailableLanguages;

    public event Action<User>? LoginSuccessful;

    public LoginViewModel(AuthService auth)
    {
        _auth = auth;
        _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == Localizer.Instance.CurrentLanguage);
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value != null) Localizer.Instance.SetLanguage(value.Code);
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = Localizer.Instance["Login_ErrorEmptyFields"];
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
                ErrorMessage = Localizer.Instance["Login_ErrorInvalid"];
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
            ErrorMessage = Localizer.Instance["Login_ErrorEmptyFields"];
            return;
        }
        LogService.Click(Username, "Admin anlegen-Button");
        IsLoading = true;
        ErrorMessage = "";
        try
        {
            await _auth.CreateUserAsync(new User
            {
                Username = Username,
                DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Username : DisplayName,
                Role = UserRole.Admin,
                Category = PersonCategory.Parent,
                Language = SelectedLanguage?.Code ?? Localizer.Instance.CurrentLanguage
            }, Password);
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
            ErrorMessage = Localizer.Instance["Login_ErrorCreateFailed"];
        }
        finally
        {
            IsLoading = false;
            Password = "";
        }
    }
}
