using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

/// <summary>In-Memory-Fake für IStorageService — Tests ohne Dateisystem.</summary>
public class InMemoryStorageService : IStorageService
{
    private List<User> _users = new();
    private AppSettings _settings = new();
    private readonly Dictionary<string, CalendarDay> _days = new();
    private List<ShiftSwapRequest> _swapRequests = new();
    private List<Notification> _notifications = new();
    private List<ActivityType> _activityTypes = new();
    private List<RecurringActivity> _recurringActivities = new();

    public Task<List<User>> LoadUsersAsync()
        // Kopie zurückgeben, damit Tests echtes Laden/Speichern abbilden
        => Task.FromResult(_users.Select(Clone).ToList());

    public Task SaveUsersAsync(List<User> users)
    {
        _users = users.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    public Task ReorderUsersAsync(IReadOnlyList<string> userIds)
    {
        for (int i = 0; i < userIds.Count; i++)
        {
            var u = _users.FirstOrDefault(x => x.Id == userIds[i]);
            if (u is not null) u.PlanOrder = i;
        }
        return Task.CompletedTask;
    }

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(_settings);
    public Task SaveSettingsAsync(AppSettings settings) { _settings = settings; return Task.CompletedTask; }

    public Task<CalendarDay> LoadDayAsync(DateOnly date)
        => Task.FromResult(_days.TryGetValue(date.ToString("yyyy-MM-dd"), out var d)
            ? d : new CalendarDay { DateString = date.ToString("yyyy-MM-dd") });

    public Task SaveDayAsync(CalendarDay day) { _days[day.DateString] = day; return Task.CompletedTask; }

    public Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync()
        => Task.FromResult(_swapRequests.Select(Clone).ToList());

    // Tausch und Benachrichtigungen laufen über denselben Helfer wie der lokale JSON-Speicher —
    // die Tests prüfen damit dessen Verhalten mit, statt eine eigene Nachbildung.

    public Task<ShiftSwapRequest> CreateSwapRequestAsync(ShiftSwapRequest request)
        => Task.FromResult(Clone(ListBackedCollaboration.Create(_swapRequests, Clone(request))));

    public Task<string?> AcceptSwapRequestAsync(ShiftSwapRequest request)
        => ListBackedCollaboration.AcceptAsync(_swapRequests, request, LoadDayAsync, SaveDayAsync);

    public Task RejectSwapRequestAsync(string id)
    {
        ListBackedCollaboration.Close(_swapRequests, id, SwapStatus.Rejected);
        return Task.CompletedTask;
    }

    public Task WithdrawSwapRequestAsync(string id)
    {
        ListBackedCollaboration.Close(_swapRequests, id, SwapStatus.Cancelled);
        return Task.CompletedTask;
    }

    /// <summary>Für Tests, die einen Ausgangsbestand brauchen.</summary>
    public void SeedSwapRequests(IEnumerable<ShiftSwapRequest> requests)
        => _swapRequests = requests.Select(Clone).ToList();

    public Task<List<Notification>> LoadNotificationsAsync(string userId)
        => Task.FromResult(ListBackedCollaboration.ForUser(_notifications, userId).Select(Clone).ToList());

    public Task AddNotificationsAsync(IReadOnlyList<Notification> notifications)
    {
        _notifications.AddRange(notifications.Select(Clone));
        return Task.CompletedTask;
    }

    public Task MarkNotificationsReadAsync(string userId, IReadOnlyCollection<string>? ids)
    {
        ListBackedCollaboration.MarkRead(_notifications, userId, ids);
        return Task.CompletedTask;
    }

    /// <summary>Kompletter Bestand quer über alle Empfänger — nur für Prüfungen in Tests.</summary>
    public IReadOnlyList<Notification> AllNotifications => _notifications.Select(Clone).ToList();

    public Task<List<ActivityType>> LoadActivityTypesAsync()
        => Task.FromResult(_activityTypes.Select(Clone).ToList());

    public Task SaveActivityTypesAsync(List<ActivityType> types)
    {
        _activityTypes = types.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    public Task<List<RecurringActivity>> LoadRecurringActivitiesAsync()
        => Task.FromResult(_recurringActivities.Select(Clone).ToList());

    public Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities)
    {
        _recurringActivities = activities.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    private List<PlannerNote> _plannerNotes = new();
    public Task<List<PlannerNote>> LoadPlannerNotesAsync()
        => Task.FromResult(_plannerNotes.Select(n => new PlannerNote { Id = n.Id, Text = n.Text, CreatedAtUtc = n.CreatedAtUtc }).ToList());
    public Task SavePlannerNotesAsync(List<PlannerNote> notes)
    {
        _plannerNotes = notes.Select(n => new PlannerNote { Id = n.Id, Text = n.Text, CreatedAtUtc = n.CreatedAtUtc }).ToList();
        return Task.CompletedTask;
    }

    private List<ChatHistoryEntry> _chatHistory = new();
    public Task<List<ChatHistoryEntry>> LoadChatHistoryAsync()
        => Task.FromResult(_chatHistory.Select(c => new ChatHistoryEntry
        { Id = c.Id, Role = c.Role, Text = c.Text, CreatedAtUtc = c.CreatedAtUtc }).ToList());
    public Task SaveChatHistoryAsync(List<ChatHistoryEntry> history)
    {
        _chatHistory = history.Select(c => new ChatHistoryEntry
        { Id = c.Id, Role = c.Role, Text = c.Text, CreatedAtUtc = c.CreatedAtUtc }).ToList();
        return Task.CompletedTask;
    }

    private static ActivityType Clone(ActivityType t) => new()
    {
        Id = t.Id, Name = t.Name, Color = t.Color, Categories = new List<PersonCategory>(t.Categories)
    };

    private static RecurringActivity Clone(RecurringActivity a) => new()
    {
        Id = a.Id, UserId = a.UserId, UserDisplayName = a.UserDisplayName, Title = a.Title,
        ActivityTypeId = a.ActivityTypeId, StartTime = a.StartTime, EndTime = a.EndTime,
        Weekdays = new List<DayOfWeek>(a.Weekdays), SkipOnHolidays = a.SkipOnHolidays
    };

    private static Notification Clone(Notification n) => new()
    {
        Id = n.Id, UserId = n.UserId, CreatedAt = n.CreatedAt, IsRead = n.IsRead,
        MessageKey = n.MessageKey, Args = new List<string>(n.Args), RelatedDate = n.RelatedDate,
        Action = n.Action, RelatedUserId = n.RelatedUserId
    };

    private static ShiftSwapRequest Clone(ShiftSwapRequest r) => new()
    {
        Id = r.Id, CreatedAt = r.CreatedAt, RespondedAt = r.RespondedAt, Status = r.Status, Mode = r.Mode,
        FromUserId = r.FromUserId, FromUserName = r.FromUserName, FromDate = r.FromDate, FromEntryId = r.FromEntryId,
        ToUserId = r.ToUserId, ToUserName = r.ToUserName, ToDate = r.ToDate, ToEntryId = r.ToEntryId,
        Message = r.Message
    };

    private static User Clone(User u) => new()
    {
        Id = u.Id, Username = u.Username, PasswordHash = u.PasswordHash, Role = u.Role,
        DisplayName = u.DisplayName, Email = u.Email, Language = u.Language,
        Category = u.Category, WeeklyHoursQuota = u.WeeklyHoursQuota,
        MaxWeeklyHours = u.MaxWeeklyHours, MaxDailyHours = u.MaxDailyHours, MinRestHours = u.MinRestHours,
        ThemeVariant = u.ThemeVariant, Color = u.Color,
        OpeningBalanceHours = u.OpeningBalanceHours, AccountStart = u.AccountStart,
        ShowHolidays = u.ShowHolidays
    };
}
