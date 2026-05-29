using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Server-gestützte Persistenz (entweder/oder zur lokalen <see cref="StorageService"/> — kein Parallelbetrieb).
/// Benutzer und Kalender laufen über die API. Flächen ohne Server-Endpunkt (Aktivitätstypen,
/// wiederkehrende Aktivitäten, Tausch, Benachrichtigungen) liefern vorerst leer/No-op — KEIN lokaler Fallback.
/// Einstellungen sind Installations-Config und werden lokal gehalten (enthalten u.a. die Server-URL).
/// </summary>
public class ApiStorageService : IStorageService
{
    private readonly ApiClient _api;
    private readonly IStorageService _settingsStore;   // nur für AppSettings (lokale Installations-Config)

    public ApiStorageService(ApiClient api, IStorageService settingsStore)
    {
        _api = api;
        _settingsStore = settingsStore;
    }

    // --- Benutzer ---------------------------------------------------------

    public async Task<List<User>> LoadUsersAsync()
    {
        try
        {
            var dtos = await _api.GetUsersAsync();
            return dtos.Select(MapUser).ToList();
        }
        catch (Exception ex)
        {
            // z.B. 403 für Nicht-Admins (Benutzerliste ist Admin-only). Slice ist Admin-orientiert;
            // damit die App nicht abstürzt, fällt sie auf den angemeldeten Benutzer zurück.
            LogService.Warn("Benutzerliste vom Server nicht ladbar: {0}", ex.Message);
            return _api.CurrentUser is { } me ? new List<User> { MapUser(me) } : new();
        }
    }

    // Voll-Benutzerverwaltung (Update/Löschen/Bulk) hat noch keinen Server-Endpunkt → No-op (TODO Endpunkte).
    public Task SaveUsersAsync(List<User> users)
    {
        LogService.Debug("SaveUsersAsync im Server-Modus übersprungen (Benutzerverwaltung folgt mit Endpunkten).");
        return Task.CompletedTask;
    }

    private static User MapUser(ServerUserDto d) => new()
    {
        Id = d.Id,
        Username = d.Username,
        DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.Username : d.DisplayName,
        Email = d.Email ?? "",
        Role = string.Equals(d.Role, "Admin", StringComparison.OrdinalIgnoreCase) ? UserRole.Admin : UserRole.User,
        Category = Enum.TryParse<PersonCategory>(d.Category, out var c) ? c : PersonCategory.Employee
    };

    // --- Einstellungen (lokal, Installations-Config) ----------------------

    public Task<AppSettings> LoadSettingsAsync() => _settingsStore.LoadSettingsAsync();
    public Task SaveSettingsAsync(AppSettings settings) => _settingsStore.SaveSettingsAsync(settings);

    // --- Kalender ---------------------------------------------------------

    public async Task<CalendarDay> LoadDayAsync(DateOnly date)
    {
        var day = new CalendarDay { DateString = date.ToString("yyyy-MM-dd") };
        var dtos = await _api.GetEntriesAsync(date, date);
        day.Entries = dtos.Select(d => EntryMapping.ToDesktop(d, date)).ToList();
        // Hinweis: Tagesnotiz/Finalisiert haben noch keinen Server-Endpunkt → bleiben leer.
        return day;
    }

    public async Task SaveDayAsync(CalendarDay day)
    {
        var date = day.Date;
        var server = await _api.GetEntriesAsync(date, date);

        // Nur persistente Einträge; Laufzeit-Projektionen (Nacht-Fortsetzung, Recurring-Overlay) nicht speichern.
        var desired = day.Entries.Where(e => !e.IsContinuation && !e.IsRecurring).ToList();

        // Löschen: Server-Einträge, zu denen kein gewünschter Eintrag mehr passt.
        foreach (var s in server)
            if (!desired.Any(e => Corresponds(s, e, date)))
                await _api.DeleteEntryAsync(s.Id);

        // Anlegen/Aktualisieren.
        foreach (var e in desired)
        {
            var match = server.FirstOrDefault(s => Corresponds(s, e, date));
            if (match is null)
                await _api.CreateEntryAsync(EntryMapping.ToCreateBody(e, date));
            else if (!EntryMapping.IsAbsenceType(e.Type))
                await _api.UpdateEntryAsync(match.Id, EntryMapping.ToUpdateBody(e, date));
            // Abwesenheit per Zeitraum schon vorhanden → nichts zu tun.
        }
    }

    /// <summary>
    /// Ordnet einen Server-Eintrag einem Desktop-Eintrag zu: Schichten über die Id, Abwesenheiten
    /// über (Benutzer, Typ, Zeitraum) — so wird ein mehrtägiger Bereich nicht je Tag dupliziert.
    /// </summary>
    private static bool Corresponds(ServerEntryDto s, CalendarEntry e, DateOnly date)
    {
        if (EntryMapping.IsAbsenceType(e.Type))
        {
            var start = e.AbsenceStart ?? date;
            var end = e.AbsenceEnd ?? start;
            return s.Type == EntryMapping.TypeToServer(e.Type)
                && s.UserId == e.UserId
                && s.Date == start
                && (s.EndDate ?? s.Date) == end;
        }
        return s.Id == e.Id;
    }

    // --- Noch ohne Server-Endpunkt: leer/No-op (kein lokaler Fallback) ----

    public Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync() => Task.FromResult(new List<ShiftSwapRequest>());
    public Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests) => Task.CompletedTask;

    public Task<List<Notification>> LoadNotificationsAsync() => Task.FromResult(new List<Notification>());
    public Task SaveNotificationsAsync(List<Notification> notifications) => Task.CompletedTask;

    public Task<List<ActivityType>> LoadActivityTypesAsync() => Task.FromResult(new List<ActivityType>());
    public Task SaveActivityTypesAsync(List<ActivityType> types) => Task.CompletedTask;

    public Task<List<RecurringActivity>> LoadRecurringActivitiesAsync() => Task.FromResult(new List<RecurringActivity>());
    public Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities) => Task.CompletedTask;
}
