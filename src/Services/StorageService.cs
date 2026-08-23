using FlexFamilyCalendar.Models;
using System.Globalization;

namespace FlexFamilyCalendar.Services;

public class StorageService : IStorageService
{
    public static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlexFamilyCalendar");

    private string UsersFile => Path.Combine(DataDirectory, "users.json");
    private string SettingsFile => Path.Combine(DataDirectory, "settings.json");
    private string SwapRequestsFile => Path.Combine(DataDirectory, "swap-requests.json");
    private string NotificationsFile => Path.Combine(DataDirectory, "notifications.json");
    private string ActivityTypesFile => Path.Combine(DataDirectory, "activity-types.json");
    private string RecurringActivitiesFile => Path.Combine(DataDirectory, "recurring-activities.json");
    private string PlannerNotesFile => Path.Combine(DataDirectory, "planner-notes.json");
    private string ChatHistoryFile => Path.Combine(DataDirectory, "chat-history.json");

    public StorageService() => Directory.CreateDirectory(DataDirectory);

    public async Task<List<User>> LoadUsersAsync()
    {
        return await JsonFileStore.LoadAsync<List<User>>(UsersFile, static () => new());
    }

    public async Task SaveUsersAsync(List<User> users)
    {
        await JsonFileStore.WriteAtomicAsync(UsersFile, users);
        LogService.Debug("Benutzerdaten gespeichert ({0} Benutzer)", users.Count);
    }

    public async Task ReorderUsersAsync(IReadOnlyList<string> userIds)
    {
        var users = await LoadUsersAsync();
        for (int i = 0; i < userIds.Count; i++)
        {
            var u = users.FirstOrDefault(x => x.Id == userIds[i]);
            if (u is not null) u.PlanOrder = i;
        }
        await SaveUsersAsync(users);
    }

    public async Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync()
    {
        return await JsonFileStore.LoadAsync<List<ShiftSwapRequest>>(SwapRequestsFile, static () => new());
    }

    public async Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
    {
        await JsonFileStore.WriteAtomicAsync(SwapRequestsFile, requests);
        LogService.Debug("Tausch-Anfragen gespeichert ({0})", requests.Count);
    }

    public async Task<List<Notification>> LoadNotificationsAsync()
    {
        return await JsonFileStore.LoadAsync<List<Notification>>(NotificationsFile, static () => new());
    }

    public async Task SaveNotificationsAsync(List<Notification> notifications)
    {
        await JsonFileStore.WriteAtomicAsync(NotificationsFile, notifications);
        LogService.Debug("Benachrichtigungen gespeichert ({0})", notifications.Count);
    }

    public async Task<List<ActivityType>> LoadActivityTypesAsync()
    {
        return await JsonFileStore.LoadAsync<List<ActivityType>>(ActivityTypesFile, static () => new());
    }

    public async Task SaveActivityTypesAsync(List<ActivityType> types)
    {
        await JsonFileStore.WriteAtomicAsync(ActivityTypesFile, types);
        LogService.Debug("Aktivitätstypen gespeichert ({0})", types.Count);
    }

    public async Task<List<RecurringActivity>> LoadRecurringActivitiesAsync()
    {
        return await JsonFileStore.LoadAsync<List<RecurringActivity>>(RecurringActivitiesFile, static () => new());
    }

    public async Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities)
    {
        await JsonFileStore.WriteAtomicAsync(RecurringActivitiesFile, activities);
        LogService.Debug("Wiederkehrende Aktivitäten gespeichert ({0})", activities.Count);
    }

    public async Task<List<PlannerNote>> LoadPlannerNotesAsync()
    {
        return await JsonFileStore.LoadAsync<List<PlannerNote>>(PlannerNotesFile, static () => new());
    }

    public async Task SavePlannerNotesAsync(List<PlannerNote> notes)
    {
        await JsonFileStore.WriteAtomicAsync(PlannerNotesFile, notes);
        LogService.Debug("KI-Planungshinweise gespeichert ({0})", notes.Count);
    }

    public async Task<List<ChatHistoryEntry>> LoadChatHistoryAsync()
    {
        return await JsonFileStore.LoadAsync<List<ChatHistoryEntry>>(ChatHistoryFile, static () => new());
    }

    public async Task SaveChatHistoryAsync(List<ChatHistoryEntry> history)
    {
        await JsonFileStore.WriteAtomicAsync(ChatHistoryFile, history);
        LogService.Debug("KI-Chat-Verlauf gespeichert ({0})", history.Count);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        return await JsonFileStore.LoadAsync<AppSettings>(SettingsFile, static () => new());
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await JsonFileStore.WriteAtomicAsync(SettingsFile, settings);
        LogService.Debug("Einstellungen gespeichert");
    }

    public async Task<CalendarDay> LoadDayAsync(DateOnly date)
    {
        var file = GetDayFilePath(date);
        var iso = date.ToString("yyyy-MM-dd");
        var day = await JsonFileStore.LoadAsync(file, () => new CalendarDay { DateString = iso });

        // Migration: ehemaliges AuPairShift (=1) → Arbeit
        foreach (var e in day.Entries)
            if ((int)e.Type == 1) e.Type = EntryType.Work;

        return day;
    }

    public async Task SaveDayAsync(CalendarDay day)
    {
        var date = DateOnly.Parse(day.DateString);
        var file = GetDayFilePath(date);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await JsonFileStore.WriteAtomicAsync(file, day);
        LogService.Debug("Kalendertag gespeichert: {0}", day.DateString);
    }

    private static string GetDayFilePath(DateOnly date)
    {
        var week = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        return Path.Combine(DataDirectory, "calendar",
            date.Year.ToString(), $"KW{week:D2}", $"{date:yyyy-MM-dd}.json");
    }
}
