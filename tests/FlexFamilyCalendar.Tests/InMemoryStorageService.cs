using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Tests;

/// <summary>In-Memory-Fake für IStorageService — Tests ohne Dateisystem.</summary>
public class InMemoryStorageService : IStorageService
{
    private List<User> _users = new();
    private AppSettings _settings = new();
    private readonly Dictionary<string, CalendarDay> _days = new();

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

    private static User Clone(User u) => new()
    {
        Id = u.Id, Username = u.Username, PasswordHash = u.PasswordHash, Role = u.Role,
        DisplayName = u.DisplayName, Email = u.Email, Language = u.Language,
        Category = u.Category, WeeklyHoursQuota = u.WeeklyHoursQuota,
        ThemeVariant = u.ThemeVariant, Color = u.Color,
        OpeningBalanceHours = u.OpeningBalanceHours, AccountStart = u.AccountStart
    };
}
