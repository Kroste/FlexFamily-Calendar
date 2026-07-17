using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Server-gestützte Persistenz (entweder/oder zur lokalen <see cref="StorageService"/> — kein Parallelbetrieb).
/// Alle Domänendaten (Benutzer, Kalender, Aktivitätstypen, wiederkehrende Aktivitäten, Schichttausch,
/// Benachrichtigungen) laufen über die API — kein lokaler Fallback. Nur <see cref="AppSettings"/> sind
/// lokale Installations-Config (enthalten u.a. die Server-URL).
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
            return dtos.Select(UserMapping.ToDesktop).ToList();
        }
        catch (Exception ex)
        {
            // z.B. 403 für Nicht-Admins (Benutzerliste ist Admin-only). Slice ist Admin-orientiert;
            // damit die App nicht abstürzt, fällt sie auf den angemeldeten Benutzer zurück.
            LogService.Warn("Benutzerliste vom Server nicht ladbar: {0}", ex.Message);
            return _api.CurrentUser is { } me ? new List<User> { UserMapping.ToDesktop(me) } : new();
        }
    }

    // Admin-Benutzerverwaltung (Anlegen/Ändern/Löschen/Passwort) läuft granular über AuthService → ApiClient,
    // nicht über diese Bulk-Methode. Im Server-Modus erreicht SaveUsersAsync nur noch Self-Präferenz-Toggles
    // (z.B. Feiertags-Anzeige) für den angemeldeten Benutzer → über den Self-Profil-Endpunkt persistieren.
    public async Task SaveUsersAsync(List<User> users)
    {
        if (_api.CurrentUser is not { } me) return;
        var self = users.FirstOrDefault(u => u.Id == me.Id);
        if (self is null) return;
        await _api.UpdateMyProfileAsync(new UpdateProfileBody(
            self.DisplayName, self.Email, self.Language, self.Color, self.AiStyleHint,
            self.ThemeVariant, self.ShowHolidays, self.ShowHints, self.OnboardingSeen));
        LogService.Debug("Self-Präferenzen des angemeldeten Benutzers via Profil-Endpunkt gespeichert.");
    }

    // --- Einstellungen (Trennung: lokal = Installations-Config, Server = Domänen-Config) --
    // Kanon: im Server-Modus MUSS die Domänen-Config (HolidayState, OvernightHoursPerDay)
    // vom Server kommen — kein stiller lokaler Fallback. Installations-Config (ServerUrl, JWT,
    // gemerkter Benutzer, EncryptedApiKeys, SMTP) bleibt lokal, weil sie pro Installation ist.
    public async Task<AppSettings> LoadSettingsAsync()
    {
        var s = await _settingsStore.LoadSettingsAsync();
        // Am Login-Screen ist noch kein Token da. GET /api/settings würde dort 401 werfen —
        // und der Aufruf steht im Init-Pfad des Browser-Heads, was den Login-View blockiert.
        // In dem Fall bleiben wir bei den lokalen Defaults.
        if (!_api.IsAuthenticated) return s;
        try
        {
            var srv = await _api.GetServerSettingsAsync();
            if (srv is not null)
            {
                s.HolidayState = srv.HolidayState;
                s.OvernightHoursPerDay = srv.OvernightHoursPerDay;
            }
        }
        catch (Exception ex)
        {
            LogService.Warn("Server-Einstellungen konnten nicht geladen werden: {0}", ex.Message);
        }
        return s;
    }

    public async Task SaveSettingsAsync(AppSettings settings)
    {
        // Lokale Installations-Config immer schreiben (auch ServerUrl-Änderungen etc.).
        await _settingsStore.SaveSettingsAsync(settings);
        // Domänen-Config zurück zum Server nur mit Token (Nicht-Admin bekommt 403 — dann
        // still schlucken, der Server bleibt Source of Truth). Ohne Token gar nicht erst
        // versuchen — würde 401 loggen.
        if (!_api.IsAuthenticated) return;
        try
        {
            await _api.UpdateServerSettingsAsync(new ServerSettingsDto(settings.HolidayState, settings.OvernightHoursPerDay));
        }
        catch (Exception ex)
        {
            LogService.Warn("Server-Einstellungen konnten nicht gespeichert werden: {0}", ex.Message);
        }
    }

    // --- Kalender ---------------------------------------------------------

    public async Task<CalendarDay> LoadDayAsync(DateOnly date)
    {
        var day = new CalendarDay { DateString = date.ToString("yyyy-MM-dd") };
        var dtos = await _api.GetEntriesAsync(date, date);
        day.Entries = dtos.Select(d => EntryMapping.ToDesktop(d, date)).ToList();
        var note = await _api.GetDayNoteAsync(date);
        day.Note = note.Note ?? "";
        day.NoteUserId = note.NoteUserId;
        day.IsFinalized = note.IsFinalized;
        return day;
    }

    public async Task SaveDayAsync(CalendarDay day)
    {
        var date = day.Date;
        var server = await _api.GetEntriesAsync(date, date);

        // Nur persistente Einträge; Recurring-Overlay-Projektionen nicht speichern.
        var desired = day.Entries.Where(e => !e.IsRecurring).ToList();

        LogService.Debug("Tag {0:yyyy-MM-dd} speichern: {1} gewünscht, {2} auf Server", date, desired.Count, server.Count);

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

        // Tagesnotiz/Finalisiert: Admin oder Eltern. Im Client setzen wir's einfach immer und
        // verlassen uns auf die Server-Policy (AdminOrParent), die nicht-erlaubten Aufrufern 403 gibt.
        if (_api.CurrentUserIsAdmin || _api.CurrentUserIsParent)
            await _api.SetDayNoteAsync(date, new ServerDayNoteDto(day.Note ?? "", day.IsFinalized, day.NoteUserId));
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

    // --- Server-Listen (Speichern ersetzt jeweils die ganze Liste) ---

    public async Task<List<ActivityType>> LoadActivityTypesAsync()
    {
        var dtos = await _api.GetActivityTypesAsync();
        return dtos.Select(ActivityTypeMapping.ToDesktop).ToList();
    }

    public Task SaveActivityTypesAsync(List<ActivityType> types)
        => _api.ReplaceActivityTypesAsync(types.Select(ActivityTypeMapping.ToServer).ToList());

    public async Task<List<RecurringActivity>> LoadRecurringActivitiesAsync()
    {
        var dtos = await _api.GetRecurringActivitiesAsync();
        return dtos.Select(RecurringActivityMapping.ToDesktop).ToList();
    }

    public Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities)
        => _api.ReplaceRecurringActivitiesAsync(activities.Select(RecurringActivityMapping.ToServer).ToList());

    public async Task<List<PlannerNote>> LoadPlannerNotesAsync()
    {
        var dtos = await _api.GetPlannerNotesAsync();
        return dtos.Select(d => new PlannerNote { Id = d.Id, Text = d.Text, CreatedAtUtc = d.CreatedAtUtc }).ToList();
    }

    public Task SavePlannerNotesAsync(List<PlannerNote> notes)
        => _api.ReplacePlannerNotesAsync(notes.Select(n => new ServerPlannerNoteDto(n.Id, n.Text, n.CreatedAtUtc)).ToList());

    public async Task<List<ChatHistoryEntry>> LoadChatHistoryAsync()
    {
        var dtos = await _api.GetChatHistoryAsync();
        return dtos.Select(d => new ChatHistoryEntry
        {
            Id = d.Id,
            Role = d.Role.Equals("Assistant", StringComparison.OrdinalIgnoreCase)
                ? ChatHistoryRole.Assistant : ChatHistoryRole.User,
            Text = d.Text,
            CreatedAtUtc = d.CreatedAtUtc
        }).ToList();
    }

    public Task SaveChatHistoryAsync(List<ChatHistoryEntry> history)
        => _api.ReplaceChatHistoryAsync(history.Select(h => new ServerChatHistoryDto(
            h.Id, h.Role.ToString(), h.Text, h.CreatedAtUtc)).ToList());

    public async Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync()
    {
        var dtos = await _api.GetSwapRequestsAsync();
        return dtos.Select(ShiftSwapMapping.ToDesktop).ToList();
    }

    public Task SaveSwapRequestsAsync(List<ShiftSwapRequest> requests)
        => _api.ReplaceSwapRequestsAsync(requests.Select(ShiftSwapMapping.ToServer).ToList());

    public async Task<List<Notification>> LoadNotificationsAsync()
    {
        var dtos = await _api.GetNotificationsAsync();
        return dtos.Select(NotificationMapping.ToDesktop).ToList();
    }

    public Task SaveNotificationsAsync(List<Notification> notifications)
        => _api.ReplaceNotificationsAsync(notifications.Select(NotificationMapping.ToServer).ToList());
}
