using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Swaps;

/// <summary>Neuer Tauschvorschlag. Namen und Datumswerte leitet der Server aus den Einträgen ab —
/// was der Client dazu schickt, wird nicht übernommen.</summary>
public record CreateSwapRequest(
    int Mode,
    string? FromUserId,
    string FromEntryId,
    string ToUserId,
    string? ToEntryId,
    string? Message);

/// <summary>
/// Schichttausch serverseitig: wer Vorschläge sieht, wer sie anlegen und beantworten darf, und ob
/// ein Tausch beim Annehmen noch gilt.
///
/// Vorher war das eine Replace-all-Liste für jeden Angemeldeten, und das Annehmen lief im Client:
/// er schrieb die <c>UserId</c> der Schicht um und speicherte den Tag. Über die API kam das nie an —
/// <c>UpdateEntryRequest</c> trägt gar keine <c>UserId</c>, und ein Mitarbeiter darf die Schicht
/// des Kollegen ohnehin nicht ändern. Nahm der Admin an, stand der Tausch auf „angenommen", die
/// Schicht wechselte aber nie den Besitzer; nahm ein Mitarbeiter an, scheiterte es mit 403.
/// </summary>
public static class SwapRules
{
    public const int Pending = 0, Accepted = 1, Rejected = 2, Cancelled = 3;
    public const int GiveAway = 0, Exchange = 1;

    // Dieselben Schlüssel wie ShiftSwapEngine im Client, damit die Meldung dort übersetzt ankommt.
    public const string ErrorStale = "Swap_ErrorStale";
    public const string ErrorFinalized = "Swap_ErrorFinalized";
    public const string ErrorOverlap = "Swap_ErrorOverlap";
    public const string ErrorNotPending = "Swap_ErrorNotPending";

    /// <summary>Beteiligte und Admin — sonst niemand. Ein Tauschwunsch samt Nachricht ist eine
    /// Sache zwischen zwei Kollegen und der Planung.</summary>
    public static bool CanSee(ShiftSwapRequestEntity r, string requesterId, bool isAdmin)
        => isAdmin || r.FromUserId == requesterId || r.ToUserId == requesterId;

    public static bool CanRespond(ShiftSwapRequestEntity r, string requesterId, bool isAdmin)
        => isAdmin || r.ToUserId == requesterId;

    public static bool CanWithdraw(ShiftSwapRequestEntity r, string requesterId, bool isAdmin)
        => isAdmin || r.FromUserId == requesterId;

    /// <summary>
    /// Prüft einen neuen Vorschlag gegen die echten Einträge. Gibt eine Fehlermeldung zurück, sonst null.
    /// Nicht-Admins dürfen nur ihre eigene Schicht anbieten; der Admin darf im Namen anderer
    /// vorschlagen (KI-Planer).
    /// </summary>
    public static string? CheckCreate(int mode, string fromUserId, string toUserId, string requesterId, bool isAdmin,
        CalendarEntry? fromEntry, CalendarEntry? toEntry)
    {
        if (mode is not (GiveAway or Exchange)) return "Unbekannter Tauschmodus.";
        if (!isAdmin && fromUserId != requesterId) return "Du kannst nur deine eigene Schicht anbieten.";
        if (fromUserId == toUserId) return "Tausch mit dir selbst ist nicht möglich.";
        if (fromEntry is null || fromEntry.UserId.ToString() != fromUserId || fromEntry.Type != EntryTypes.Work)
            return "Die angebotene Schicht gibt es so nicht.";
        if (mode == Exchange
            && (toEntry is null || toEntry.UserId.ToString() != toUserId || toEntry.Type != EntryTypes.Work))
            return "Die Gegen-Schicht gibt es so nicht.";
        return null;
    }

    /// <summary>
    /// Prüft beim Annehmen, ob der Tausch noch gilt: Schichten noch da und noch bei denselben
    /// Personen, Tage nicht finalisiert, keine Überschneidung danach. Gleiche Regeln wie
    /// <c>ShiftSwapEngine.Validate</c> im Client — der Server darf sich auf dessen Prüfung nicht verlassen.
    /// </summary>
    /// <param name="workOnAffectedDays">Alle Arbeitsschichten beider Personen an den betroffenen Tagen.</param>
    public static string? ValidateAccept(ShiftSwapRequestEntity r, CalendarEntry? fromEntry, CalendarEntry? toEntry,
        IReadOnlyCollection<CalendarEntry> workOnAffectedDays, IReadOnlySet<DateOnly> finalizedDays)
    {
        if (r.Status != Pending) return ErrorNotPending;
        if (fromEntry is null || fromEntry.UserId.ToString() != r.FromUserId) return ErrorStale;
        if (finalizedDays.Contains(fromEntry.Date)) return ErrorFinalized;

        var assignments = new Dictionary<Guid, string> { [fromEntry.Id] = r.ToUserId };
        var affectedDays = new HashSet<DateOnly> { fromEntry.Date };

        if (r.Mode == Exchange)
        {
            if (toEntry is null || toEntry.UserId.ToString() != r.ToUserId) return ErrorStale;
            if (finalizedDays.Contains(toEntry.Date)) return ErrorFinalized;
            assignments[toEntry.Id] = r.FromUserId;
            affectedDays.Add(toEntry.Date);
        }

        foreach (var day in affectedDays)
            foreach (var userId in new[] { r.FromUserId, r.ToUserId })
            {
                var after = workOnAffectedDays
                    .Where(e => e.Date == day && e.Type == EntryTypes.Work)
                    .Where(e => assignments.GetValueOrDefault(e.Id, e.UserId.ToString()) == userId)
                    .ToList();
                if (Overlaps(after)) return ErrorOverlap;
            }

        return null;
    }

    /// <summary>Dieselbe Überschneidungsregel wie <c>WorkTimeRules.WorkOverlaps</c> im Client.</summary>
    private static bool Overlaps(List<CalendarEntry> work)
    {
        for (var i = 0; i < work.Count; i++)
            for (var j = i + 1; j < work.Count; j++)
            {
                var a = work[i];
                var b = work[j];
                if (a.StartTime is not { } aStart || a.EndTime is not { } aEnd
                    || b.StartTime is not { } bStart || b.EndTime is not { } bEnd) continue;
                if (aStart < bEnd && bStart < aEnd) return true;
            }
        return false;
    }
}
