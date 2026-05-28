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

    public Task<List<User>> LoadUsersAsync()
        // Kopie zurückgeben, damit Tests echtes Laden/Speichern abbilden
        => Task.FromResult(_users.Select(Clone).ToList());

    public Task SaveUsersAsync(List<User> users)
    {
        _users = users.Select(Clone).ToList();
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

    public Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
    {
        _swapRequests = requests.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    public Task<List<Notification>> LoadNotificationsAsync()
        => Task.FromResult(_notifications.Select(Clone).ToList());

    public Task SaveNotificationsAsync(List<Notification> notifications)
    {
        _notifications = notifications.Select(Clone).ToList();
        return Task.CompletedTask;
    }

    private static Notification Clone(Notification n) => new()
    {
        Id = n.Id, UserId = n.UserId, CreatedAt = n.CreatedAt, IsRead = n.IsRead,
        MessageKey = n.MessageKey, Args = new List<string>(n.Args), RelatedDate = n.RelatedDate
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
        OpeningBalanceHours = u.OpeningBalanceHours, AccountStart = u.AccountStart
    };
}
