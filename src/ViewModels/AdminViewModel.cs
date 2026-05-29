using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Bündelt die Admin-Verwaltung in Tabs: Benutzer, Kategorien, wiederkehrende Aktivitäten, Einstellungen, KI.</summary>
public class AdminViewModel : ViewModelBase
{
    public UserManagementViewModel Users { get; }
    public ActivityTypeManagementViewModel Categories { get; }
    public RecurringActivityManagementViewModel Recurring { get; }
    public SettingsViewModel Settings { get; }
    public AiSettingsViewModel Ai { get; }

    public AdminViewModel(AuthService auth, IStorageService storage, AiService ai)
    {
        Users = new UserManagementViewModel(auth);
        Categories = new ActivityTypeManagementViewModel(storage);
        Recurring = new RecurringActivityManagementViewModel(storage);
        Settings = new SettingsViewModel(storage);
        Ai = new AiSettingsViewModel(ai, storage);
    }
}
