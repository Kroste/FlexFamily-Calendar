using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Abstraktion der Persistenz — austauschbar (Dateisystem / später WASM) und testbar.</summary>
public interface IStorageService
{
    Task<List<User>> LoadUsersAsync();
    Task SaveUsersAsync(List<User> users);
    /// <summary>
    /// Personen-Reihenfolge im Plan setzen (Admin-Aktion). Die übergebene ID-Reihenfolge ist die
    /// gewünschte Anzeigereihenfolge; der Backing-Store setzt <c>PlanOrder</c> entsprechend.
    /// </summary>
    Task ReorderUsersAsync(IReadOnlyList<string> userIds);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<CalendarDay> LoadDayAsync(DateOnly date);
    Task SaveDayAsync(CalendarDay day);
    Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync();
    Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests);
    Task<List<Notification>> LoadNotificationsAsync();
    Task SaveNotificationsAsync(List<Notification> notifications);
    Task<List<ActivityType>> LoadActivityTypesAsync();
    Task SaveActivityTypesAsync(List<ActivityType> types);
    Task<List<RecurringActivity>> LoadRecurringActivitiesAsync();
    Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities);
    Task<List<PlannerNote>> LoadPlannerNotesAsync();
    Task SavePlannerNotesAsync(List<PlannerNote> notes);
    Task<List<ChatHistoryEntry>> LoadChatHistoryAsync();
    Task SaveChatHistoryAsync(List<ChatHistoryEntry> history);
}
