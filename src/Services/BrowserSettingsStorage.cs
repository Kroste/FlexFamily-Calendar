using FlexFamilyCalendar.Models;
using Newtonsoft.Json;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// IStorageService-Adapter für den Browser: nur <see cref="LoadSettingsAsync"/>/<see cref="SaveSettingsAsync"/>
/// sind echt implementiert (über einen <see cref="IBrowserKeyValueStore"/>, typischerweise localStorage).
/// Domänendaten laufen ohnehin über <see cref="ApiStorageService"/>; die übrigen Methoden werden vom
/// ApiStorageService nie aufgerufen und werfen daher <see cref="NotSupportedException"/>.
/// </summary>
public class BrowserSettingsStorage : IStorageService
{
    private const string Key = "ffc.settings";
    private readonly IBrowserKeyValueStore _store;
    private AppSettings _cached;

    public BrowserSettingsStorage(IBrowserKeyValueStore store)
    {
        _store = store;
        var raw = store.Get(Key);
        _cached = string.IsNullOrEmpty(raw)
            ? new AppSettings()
            : (JsonConvert.DeserializeObject<AppSettings>(raw) ?? new AppSettings());
    }

    public Task<AppSettings> LoadSettingsAsync() => Task.FromResult(_cached);

    public Task SaveSettingsAsync(AppSettings settings)
    {
        _cached = settings;
        _store.Set(Key, JsonConvert.SerializeObject(settings));
        return Task.CompletedTask;
    }

    // ApiStorageService nutzt den settingsStore NUR für AppSettings — alle anderen Methoden bleiben ungenutzt.
    public Task<List<User>> LoadUsersAsync() => throw new NotSupportedException();
    public Task SaveUsersAsync(List<User> users) => throw new NotSupportedException();
    public Task<CalendarDay> LoadDayAsync(DateOnly date) => throw new NotSupportedException();
    public Task SaveDayAsync(CalendarDay day) => throw new NotSupportedException();
    public Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync() => throw new NotSupportedException();
    public Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests) => throw new NotSupportedException();
    public Task<List<Notification>> LoadNotificationsAsync() => throw new NotSupportedException();
    public Task SaveNotificationsAsync(List<Notification> notifications) => throw new NotSupportedException();
    public Task<List<ActivityType>> LoadActivityTypesAsync() => throw new NotSupportedException();
    public Task SaveActivityTypesAsync(List<ActivityType> types) => throw new NotSupportedException();
    public Task<List<RecurringActivity>> LoadRecurringActivitiesAsync() => throw new NotSupportedException();
    public Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities) => throw new NotSupportedException();
}
