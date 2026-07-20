using FlexFamilyCalendar.Models;
using System.Globalization;
using System.Text.Json;

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
        if (!File.Exists(UsersFile)) return new();
        var json = await File.ReadAllTextAsync(UsersFile);
        return JsonSerializer.Deserialize<List<User>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveUsersAsync(List<User> users)
    {
        await File.WriteAllTextAsync(UsersFile, JsonSerializer.Serialize(users, JsonOptions.Pretty));
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
        if (!File.Exists(SwapRequestsFile)) return new();
        var json = await File.ReadAllTextAsync(SwapRequestsFile);
        return JsonSerializer.Deserialize<List<ShiftSwapRequest>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
    {
        await File.WriteAllTextAsync(SwapRequestsFile, JsonSerializer.Serialize(requests, JsonOptions.Pretty));
        LogService.Debug("Tausch-Anfragen gespeichert ({0})", requests.Count);
    }

    public async Task<List<Notification>> LoadNotificationsAsync()
    {
        if (!File.Exists(NotificationsFile)) return new();
        var json = await File.ReadAllTextAsync(NotificationsFile);
        return JsonSerializer.Deserialize<List<Notification>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveNotificationsAsync(List<Notification> notifications)
    {
        await File.WriteAllTextAsync(NotificationsFile, JsonSerializer.Serialize(notifications, JsonOptions.Pretty));
        LogService.Debug("Benachrichtigungen gespeichert ({0})", notifications.Count);
    }

    public async Task<List<ActivityType>> LoadActivityTypesAsync()
    {
        if (!File.Exists(ActivityTypesFile)) return new();
        var json = await File.ReadAllTextAsync(ActivityTypesFile);
        return JsonSerializer.Deserialize<List<ActivityType>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveActivityTypesAsync(List<ActivityType> types)
    {
        await File.WriteAllTextAsync(ActivityTypesFile, JsonSerializer.Serialize(types, JsonOptions.Pretty));
        LogService.Debug("Aktivitätstypen gespeichert ({0})", types.Count);
    }

    public async Task<List<RecurringActivity>> LoadRecurringActivitiesAsync()
    {
        if (!File.Exists(RecurringActivitiesFile)) return new();
        var json = await File.ReadAllTextAsync(RecurringActivitiesFile);
        return JsonSerializer.Deserialize<List<RecurringActivity>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities)
    {
        await File.WriteAllTextAsync(RecurringActivitiesFile, JsonSerializer.Serialize(activities, JsonOptions.Pretty));
        LogService.Debug("Wiederkehrende Aktivitäten gespeichert ({0})", activities.Count);
    }

    public async Task<List<PlannerNote>> LoadPlannerNotesAsync()
    {
        if (!File.Exists(PlannerNotesFile)) return new();
        var json = await File.ReadAllTextAsync(PlannerNotesFile);
        return JsonSerializer.Deserialize<List<PlannerNote>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SavePlannerNotesAsync(List<PlannerNote> notes)
    {
        await File.WriteAllTextAsync(PlannerNotesFile, JsonSerializer.Serialize(notes, JsonOptions.Pretty));
        LogService.Debug("KI-Planungshinweise gespeichert ({0})", notes.Count);
    }

    public async Task<List<ChatHistoryEntry>> LoadChatHistoryAsync()
    {
        if (!File.Exists(ChatHistoryFile)) return new();
        var json = await File.ReadAllTextAsync(ChatHistoryFile);
        return JsonSerializer.Deserialize<List<ChatHistoryEntry>>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveChatHistoryAsync(List<ChatHistoryEntry> history)
    {
        await File.WriteAllTextAsync(ChatHistoryFile, JsonSerializer.Serialize(history, JsonOptions.Pretty));
        LogService.Debug("KI-Chat-Verlauf gespeichert ({0})", history.Count);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsFile)) return new();
        var json = await File.ReadAllTextAsync(SettingsFile);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions.Pretty) ?? new();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await File.WriteAllTextAsync(SettingsFile, JsonSerializer.Serialize(settings, JsonOptions.Pretty));
        LogService.Debug("Einstellungen gespeichert");
    }

    public async Task<CalendarDay> LoadDayAsync(DateOnly date)
    {
        var file = GetDayFilePath(date);
        if (!File.Exists(file))
            return new() { DateString = date.ToString("yyyy-MM-dd") };
        var json = await File.ReadAllTextAsync(file);
        var day = JsonSerializer.Deserialize<CalendarDay>(json, JsonOptions.Pretty)
                  ?? new() { DateString = date.ToString("yyyy-MM-dd") };

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
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(day, JsonOptions.Pretty));
        LogService.Debug("Kalendertag gespeichert: {0}", day.DateString);
    }

    private static string GetDayFilePath(DateOnly date)
    {
        var week = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        return Path.Combine(DataDirectory, "calendar",
            date.Year.ToString(), $"KW{week:D2}", $"{date:yyyy-MM-dd}.json");
    }
}
