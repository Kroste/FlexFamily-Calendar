using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>Abstraktion der Persistenz — austauschbar (Dateisystem / später WASM) und testbar.</summary>
public interface IStorageService
{
    Task<List<User>> LoadUsersAsync();
    Task SaveUsersAsync(List<User> users);
    /// <summary>
    /// Personen-Reihenfolge im Plan setzen (Admin-Aktion). Die übergebene ID-Reihenfolge ist die
    /// gewünschte Anzeigereihenfolge; der Backing-Store setzt <c>PlanOrder</c> entsprechend.
    /// </summary>
    Task ReorderUsersAsync(IReadOnlyList<string> userIds);
    Task<AppSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(AppSettings settings);
    Task<CalendarDay> LoadDayAsync(DateOnly date);

    /// <summary>
    /// Lädt einen zusammenhängenden Zeitraum. Die Vorgabe ruft <see cref="LoadDayAsync"/> je Tag
    /// parallel auf; der Server-Modus überschreibt sie mit zwei Bereichs-Anfragen für die ganze
    /// Woche statt vierzehn Einzelaufrufen. Auf Mobilfunk dominiert die Latenz, nicht die
    /// Datenmenge — dort ist das der Unterschied zwischen „springt" und „lädt".
    /// </summary>
    async Task<IReadOnlyList<CalendarDay>> LoadDaysAsync(DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var tasks = new List<Task<CalendarDay>>();
        for (var d = from; d <= to; d = d.AddDays(1)) tasks.Add(LoadDayAsync(d));
        return await Task.WhenAll(tasks);
    }
    Task SaveDayAsync(CalendarDay day);

    // ─── Schichttausch ───
    // Einzelne Operationen statt „ganze Liste speichern": im Server-Modus sieht jeder nur die
    // Vorschläge, an denen er beteiligt ist, und angenommen wird serverseitig. Mit der alten
    // Replace-all-Liste wechselte die Schicht beim Annehmen nie den Besitzer — das
    // Eintrags-Update der API trägt gar keine UserId.

    /// <summary>Vorschläge, die der aktuelle Benutzer sehen darf (Server: Beteiligte + Admin).</summary>
    Task<List<ShiftSwapRequest>> LoadSwapRequestsAsync();

    /// <summary>Legt einen Vorschlag an und gibt ihn so zurück, wie er gespeichert wurde
    /// (im Server-Modus mit Id, Namen und Datum aus der Datenbank).</summary>
    Task<ShiftSwapRequest> CreateSwapRequestAsync(ShiftSwapRequest request);

    /// <summary>Nimmt an und bucht die Schicht(en) um. <c>null</c> = erledigt, sonst ein
    /// i18n-Fehlerschlüssel (Swap_ErrorStale/-Finalized/-Overlap/-NotPending).</summary>
    Task<string?> AcceptSwapRequestAsync(ShiftSwapRequest request);

    Task RejectSwapRequestAsync(string id);
    Task WithdrawSwapRequestAsync(string id);

    // ─── Benachrichtigungen ───
    // Lesen nur für einen Empfänger. Vorher gab es nur „alle lesen / alle ersetzen", und im
    // Server-Modus bekam jeder Client alle Benachrichtigungen aller Nutzer — samt
    // „X hat sich krank gemeldet".

    /// <summary>Benachrichtigungen dieses Empfängers (Server: immer des angemeldeten Benutzers).</summary>
    Task<List<Notification>> LoadNotificationsAsync(string userId);

    /// <summary>Hängt neue Benachrichtigungen an — auch für andere Empfänger.</summary>
    Task AddNotificationsAsync(IReadOnlyList<Notification> notifications);

    /// <summary>Markiert eigene als gelesen; <paramref name="ids"/> null = alle eigenen.</summary>
    Task MarkNotificationsReadAsync(string userId, IReadOnlyCollection<string>? ids);

    Task<List<ActivityType>> LoadActivityTypesAsync();
    Task SaveActivityTypesAsync(List<ActivityType> types);
    Task<List<RecurringActivity>> LoadRecurringActivitiesAsync();
    Task SaveRecurringActivitiesAsync(List<RecurringActivity> activities);
    Task<List<PlannerNote>> LoadPlannerNotesAsync();
    Task SavePlannerNotesAsync(List<PlannerNote> notes);
    Task<List<ChatHistoryEntry>> LoadChatHistoryAsync();
    Task SaveChatHistoryAsync(List<ChatHistoryEntry> history);
}
