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

    public async Task<ShiftSwapRequest> CreateSwapRequestAsync(ShiftSwapRequest request)
    {
        var all = await LoadSwapRequestsAsync();
        var created = ListBackedCollaboration.Create(all, request);
        await SaveSwapRequestsAsync(all);
        return created;
    }

    public async Task<string?> AcceptSwapRequestAsync(ShiftSwapRequest request)
    {
        var all = await LoadSwapRequestsAsync();
        var error = await ListBackedCollaboration.AcceptAsync(all, request, LoadDayAsync, SaveDayAsync);
        if (error is null) await SaveSwapRequestsAsync(all);
        return error;
    }

    public Task RejectSwapRequestAsync(string id) => CloseSwapAsync(id, SwapStatus.Rejected);
    public Task WithdrawSwapRequestAsync(string id) => CloseSwapAsync(id, SwapStatus.Cancelled);

    private async Task CloseSwapAsync(string id, SwapStatus status)
    {
        var all = await LoadSwapRequestsAsync();
        ListBackedCollaboration.Close(all, id, status);
        await SaveSwapRequestsAsync(all);
    }

    private async Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
    {
        await JsonFileStore.WriteAtomicAsync(SwapRequestsFile, requests);
        LogService.Debug("Tausch-Anfragen gespeichert ({0})", requests.Count);
    }

    public async Task<List<Notification>> LoadNotificationsAsync(string userId)
        => ListBackedCollaboration.ForUser(await LoadAllNotificationsAsync(), userId);

    public async Task AddNotificationsAsync(IReadOnlyList<Notification> notifications)
    {
        if (notifications.Count == 0) return;
        var all = await LoadAllNotificationsAsync();
        all.AddRange(notifications);
        await SaveNotificationsAsync(all);
    }

    public async Task MarkNotificationsReadAsync(string userId, IReadOnlyCollection<string>? ids)
    {
        var all = await LoadAllNotificationsAsync();
        if (ListBackedCollaboration.MarkRead(all, userId, ids)) await SaveNotificationsAsync(all);
    }

    private Task<List<Notification>> LoadAllNotificationsAsync()
        => JsonFileStore.LoadAsync<List<Notification>>(NotificationsFile, static () => new());

    private async Task SaveNotificationsAsync(List<Notification> notifications)
    {
        await JsonFileStore.WriteAtomicAsync(NotificationsFile, notifications);
        LogService.Debug("Benachrichtigungen gespeichert ({0})", notifications.Count);
    }

    /// <summary>Die alten Kategorien — nur noch gelesen, um Altbestand zu übernehmen. Einmal je Lauf.</summary>
    private async Task<IReadOnlyList<LegacyCategory>> LoadLegacyCategoriesAsync()
        => _legacyCategories ??= await JsonFileStore.LoadAsync<List<LegacyCategory>>(ActivityTypesFile, static () => new());

    private IReadOnlyList<LegacyCategory>? _legacyCategories;

    public async Task<List<RecurringActivity>> LoadRecurringActivitiesAsync()
    {
        var rules = await JsonFileStore.LoadAsync<List<RecurringActivity>>(RecurringActivitiesFile, static () => new());
        if (rules.Any(r => r.LegacyActivityTypeId is not null)
            && LegacyCategoryMigration.ApplyToRules(rules, await LoadLegacyCategoriesAsync()))
        {
            await SaveRecurringActivitiesAsync(rules);
            LogService.Info("Serien: Kategorienamen als Bezeichnung übernommen ({0} Einträge)", rules.Count);
        }
        return rules;
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

        // Migration: Kategorie → Bezeichnung + eigene Farbe (Kategorien gibt es seit v0.21 nicht mehr).
        // Tageweise beim ersten Laden; danach steht das alte Feld nicht mehr in der Datei.
        if (day.Entries.Any(e => e.LegacyActivityTypeId is not null)
            && LegacyCategoryMigration.ApplyToEntries(day.Entries, await LoadLegacyCategoriesAsync()))
        {
            await SaveDayAsync(day);
            LogService.Info("Kalendertag {0}: Kategorien als Bezeichnung und Farbe übernommen", iso);
        }

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
