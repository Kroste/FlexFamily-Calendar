using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Theming;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public record RoleOption(UserRole Role, string Label);
public record CategoryOption(PersonCategory Category, string Label);

public enum UserEditorAction { Saved, Deleted }
public record UserEditorResult(UserEditorAction Action, User User);

public partial class UserEditorViewModel : ViewModelBase
{
    private readonly AuthService _auth;
    private readonly User _user;
    private readonly bool _isNew;
    private readonly string _originalVariant;
    private bool _initialized;

    [ObservableProperty] private string _username;
    [ObservableProperty] private string _displayName;
    [ObservableProperty] private string _email;
    [ObservableProperty] private RoleOption? _selectedRole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRoleEditable))]
    [NotifyPropertyChangedFor(nameof(ShowPassword))]
    [NotifyPropertyChangedFor(nameof(IsPasswordRequired))]
    private CategoryOption? _selectedCategory;

    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private string _weeklyHours;
    [ObservableProperty] private string _openingBalance;
    [ObservableProperty] private DateTimeOffset? _accountStart;
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private ThemeVariantOption? _selectedThemeVariant;
    [ObservableProperty] private string? _selectedColor;

    public bool IsSelfMode { get; }
    public bool CanEditAdminFields => !IsSelfMode;
    public bool IsRoleEditable => CanEditAdminFields && SelectedCategory?.Category != PersonCategory.Parent;
    public bool ShowPassword => SelectedCategory?.Category != PersonCategory.Child;
    public bool IsPasswordRequired => _isNew && SelectedCategory?.Category != PersonCategory.Child;
    public bool CanDelete { get; }
    public string HeaderText => Localizer.Instance[_isNew ? "User_New" : "User_Edit"];
    public string PasswordHint => Localizer.Instance[_isNew ? "User_PasswordRequired" : "User_PasswordKeep"];

    public IReadOnlyList<RoleOption> Roles { get; }
    public IReadOnlyList<CategoryOption> Categories { get; }
    public IReadOnlyList<LanguageOption> Languages => Localizer.Instance.AvailableLanguages;
    public IReadOnlyList<ThemeVariantOption> ThemeVariants => ThemeManager.Instance.AvailableVariants;
    public IReadOnlyList<string> PersonColors => UserColorPalette.Colors;

    public event Action<UserEditorResult?>? Closed;

    public UserEditorViewModel(AuthService auth, User? user, bool isNew, bool selfMode)
    {
        _auth = auth;
        _isNew = isNew;
        IsSelfMode = selfMode;
        _user = user ?? new User();
        CanDelete = !isNew && !selfMode;

        Roles = Enum.GetValues<UserRole>()
            .Select(r => new RoleOption(r, Localizer.Instance[$"User_Role{r}"])).ToList();
        Categories = Enum.GetValues<PersonCategory>()
            .Select(c => new CategoryOption(c, Localizer.Instance[$"PersonCategory_{c}"])).ToList();

        _username = _user.Username;
        _displayName = _user.DisplayName;
        _email = _user.Email;
        _selectedRole = Roles.FirstOrDefault(r => r.Role == _user.Role) ?? Roles[0];
        _selectedCategory = Categories.FirstOrDefault(c => c.Category == _user.Category) ?? Categories[0];
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == _user.Language) ?? Languages.FirstOrDefault();
        _weeklyHours = _user.WeeklyHoursQuota.ToString(CultureInfo.CurrentCulture);
        _openingBalance = _user.OpeningBalanceHours.ToString(CultureInfo.CurrentCulture);
        _accountStart = _user.AccountStart.Year >= 2000
            ? new DateTimeOffset(_user.AccountStart.ToDateTime(TimeOnly.MinValue))
            : null;

        _originalVariant = string.IsNullOrEmpty(_user.ThemeVariant) ? "System" : _user.ThemeVariant;
        _selectedThemeVariant = ThemeVariants.FirstOrDefault(v => v.Id == _originalVariant) ?? ThemeVariants[0];
        _selectedColor = PersonColors.FirstOrDefault(c => c.Equals(_user.Color, StringComparison.OrdinalIgnoreCase))
                         ?? (string.IsNullOrEmpty(_user.Color) ? PersonColors[0] : _user.Color);
        _initialized = true;
    }

    partial void OnSelectedCategoryChanged(CategoryOption? value)
    {
        if (value?.Category == PersonCategory.Parent)
            SelectedRole = Roles.FirstOrDefault(r => r.Role == UserRole.Admin);
    }

    // Theme-Variante im Self-Modus sofort als Vorschau anwenden
    partial void OnSelectedThemeVariantChanged(ThemeVariantOption? value)
    {
        if (_initialized && IsSelfMode)
            ThemeManager.Instance.Apply(SelectedThemeVariant?.Id);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = "";
        if (CanEditAdminFields && string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = Localizer.Instance["User_ErrorNoUsername"];
            return;
        }
        if (IsPasswordRequired && string.IsNullOrEmpty(Password))
        {
            ErrorMessage = Localizer.Instance["User_ErrorNoPassword"];
            return;
        }
        if (!double.TryParse(WeeklyHours, NumberStyles.Any, CultureInfo.CurrentCulture, out var hours) || hours < 0)
            hours = 0;
        if (!double.TryParse(OpeningBalance, NumberStyles.Any, CultureInfo.CurrentCulture, out var opening))
            opening = 0;
        var accountStart = AccountStart.HasValue
            ? DateOnly.FromDateTime(AccountStart.Value.Date)
            : _user.AccountStart;

        var target = new User
        {
            Id = _user.Id,
            Username = CanEditAdminFields ? Username.Trim() : _user.Username,
            DisplayName = DisplayName.Trim(),
            Email = Email.Trim(),
            Role = CanEditAdminFields ? (SelectedRole?.Role ?? _user.Role) : _user.Role,
            Category = CanEditAdminFields ? (SelectedCategory?.Category ?? _user.Category) : _user.Category,
            Language = SelectedLanguage?.Code ?? _user.Language,
            WeeklyHoursQuota = CanEditAdminFields ? hours : _user.WeeklyHoursQuota,
            OpeningBalanceHours = CanEditAdminFields ? opening : _user.OpeningBalanceHours,
            AccountStart = CanEditAdminFields ? accountStart : _user.AccountStart,
            ThemeVariant = SelectedThemeVariant?.Id ?? _user.ThemeVariant,
            Color = SelectedColor ?? _user.Color,
            PasswordHash = _user.PasswordHash
        };

        try
        {
            if (_isNew)
            {
                await _auth.CreateUserAsync(target, Password);
            }
            else
            {
                await _auth.UpdateUserAsync(target);
                if (!string.IsNullOrEmpty(Password))
                    await _auth.SetPasswordAsync(target.Id, Password);
            }
            Closed?.Invoke(new UserEditorResult(UserEditorAction.Saved, target));
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler beim Speichern des Benutzers", ex);
            ErrorMessage = ex.Message;
        }
        finally
        {
            Password = "";
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!CanDelete) return;
        try
        {
            await _auth.DeleteUserAsync(_user.Id);
            Closed?.Invoke(new UserEditorResult(UserEditorAction.Deleted, _user));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Theme-Vorschau im Self-Modus zurücknehmen
        if (IsSelfMode) ThemeManager.Instance.Apply(_originalVariant);
        Closed?.Invoke(null);
    }
}
