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
    private string SwapRequestsFile => Path.Combine(DataDirectory, "swap-requests.json");
    private string NotificationsFile => Path.Combine(DataDirectory, "notifications.json");

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

    public async Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync()
    {
        if (!File.Exists(SwapRequestsFile)) return new();
        var json = await File.ReadAllTextAsync(SwapRequestsFile);
        return JsonConvert.DeserializeObject<List<ShiftSwapRequest>>(json) ?? new();
    }

    public async Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
    {
        await File.WriteAllTextAsync(SwapRequestsFile, JsonConvert.SerializeObject(requests, Formatting.Indented));
        LogService.Debug("Tausch-Anfragen gespeichert ({0})", requests.Count);
    }

    public async Task<List<Notification>> LoadNotificationsAsync()
    {
        if (!File.Exists(NotificationsFile)) return new();
        var json = await File.ReadAllTextAsync(NotificationsFile);
        return JsonConvert.DeserializeObject<List<Notification>>(json) ?? new();
    }

    public async Task SaveNotificationsAsync(List<Notification> notifications)
    {
        await File.WriteAllTextAsync(NotificationsFile, JsonConvert.SerializeObject(notifications, Formatting.Indented));
        LogService.Debug("Benachrichtigungen gespeichert ({0})", notifications.Count);
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
        var day = JsonConvert.DeserializeObject<CalendarDay>(json)
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
