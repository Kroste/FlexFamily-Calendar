using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Abstraktion der Persistenz — austauschbar (Dateisystem / später WASM) und testbar.</summary>
public interface IStorageService
{
    Task<List<User>> LoadUsersAsync();
    Task SaveUsersAsync(List<User> users);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<CalendarDay> LoadDayAsync(DateOnly date);
    Task SaveDayAsync(CalendarDay day);
}
