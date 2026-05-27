using FlexFamilyCalendar.Models;
using Newtonsoft.Json;
using System.Globalization;

namespace FlexFamilyCalendar.Services;

public class StorageService : IStorageService
{
    public static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FlexFamilyCalendar");

    private string UsersFile => Path.Combine(DataDirectory, "users.json");
    private string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public StorageService() => Directory.CreateDirectory(DataDirectory);

    public async Task<List<User>> LoadUsersAsync()
    {
        if (!File.Exists(UsersFile)) return new();
        var json = await File.ReadAllTextAsync(UsersFile);
        return JsonConvert.DeserializeObject<List<User>>(json) ?? new();
    }

    public async Task SaveUsersAsync(List<User> users)
    {
        await File.WriteAllTextAsync(UsersFile, JsonConvert.SerializeObject(users, Formatting.Indented));
        LogService.Debug("Benutzerdaten gespeichert ({0} Benutzer)", users.Count);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (!File.Exists(SettingsFile)) return new();
        var json = await File.ReadAllTextAsync(SettingsFile);
        return JsonConvert.DeserializeObject<AppSettings>(json) ?? new();
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        await File.WriteAllTextAsync(SettingsFile, JsonConvert.SerializeObject(settings, Formatting.Indented));
        LogService.Debug("Einstellungen gespeichert");
    }

    public async Task<CalendarDay> LoadDayAsync(DateOnly date)
    {
        var file = GetDayFilePath(date);
        if (!File.Exists(file))
            return new() { DateString = date.ToString("yyyy-MM-dd") };
        var json = await File.ReadAllTextAsync(file);
        return JsonConvert.DeserializeObject<CalendarDay>(json)
               ?? new() { DateString = date.ToString("yyyy-MM-dd") };
    }

    public async Task SaveDayAsync(CalendarDay day)
    {
        var date = DateOnly.Parse(day.DateString);
        var file = GetDayFilePath(date);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonConvert.SerializeObject(day, Formatting.Indented));
        LogService.Debug("Kalendertag gespeichert: {0}", day.DateString);
    }

    private static string GetDayFilePath(DateOnly date)
    {
        var week = ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));
        return Path.Combine(DataDirectory, "calendar",
            date.Year.ToString(), $"KW{week:D2}", $"{date:yyyy-MM-dd}.json");
    }
}
