using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services;

/// <summary>
/// Schichttausch und Benachrichtigungen für Speicher OHNE Server — die lokalen JSON-Dateien und
/// der Test-Fake. Beide halten den kompletten Bestand als Liste; es gibt keine Sicherheitsgrenze,
/// die irgendetwas filtern müsste. Im Server-Modus erledigt dasselbe die API
/// (Beteiligten-Filter, Annehmen serverseitig).
///
/// Gemeinsam statt doppelt, damit lokaler Modus und Tests nicht auseinanderlaufen.
/// </summary>
public static class ListBackedCollaboration
{
    public static ShiftSwapRequest Create(List<ShiftSwapRequest> all, ShiftSwapRequest request)
    {
        if (string.IsNullOrEmpty(request.Id)) request.Id = Guid.NewGuid().ToString();
        request.Status = SwapStatus.Pending;
        request.RespondedAt = null;
        all.Add(request);
        return request;
    }

    /// <summary>
    /// Nimmt an und bucht über <see cref="ShiftSwapEngine"/> um. Der Status wird in <paramref name="all"/>
    /// gesetzt; das Speichern der Liste bleibt beim Aufrufer.
    /// </summary>
    /// <returns><c>null</c> = erledigt, sonst ein i18n-Fehlerschlüssel.</returns>
    public static async Task<string?> AcceptAsync(List<ShiftSwapRequest> all, ShiftSwapRequest request,
        Func<DateOnly, Task<CalendarDay>> loadDay, Func<CalendarDay, Task> saveDay)
    {
        var stored = all.FirstOrDefault(r => r.Id == request.Id);
        if (stored is null) return "Swap_ErrorStale";
        if (stored.Status != SwapStatus.Pending) return "Swap_ErrorNotPending";

        var fromDay = await loadDay(DateOnly.Parse(stored.FromDate));
        CalendarDay? toDay = null;
        if (stored.Mode == SwapMode.Exchange && !string.IsNullOrEmpty(stored.ToDate))
            toDay = stored.ToDate == stored.FromDate ? fromDay : await loadDay(DateOnly.Parse(stored.ToDate));

        if (ShiftSwapEngine.Validate(stored, fromDay, toDay) is { } error) return error;

        ShiftSwapEngine.Apply(stored, fromDay, toDay);
        await saveDay(fromDay);
        if (toDay is not null && !ReferenceEquals(toDay, fromDay)) await saveDay(toDay);

        stored.Status = SwapStatus.Accepted;
        stored.RespondedAt = DateTime.Now;
        return null;
    }

    /// <summary>Ablehnen oder Zurückziehen. Wirft wie der Server, wenn der Vorschlag schon erledigt ist.</summary>
    public static void Close(List<ShiftSwapRequest> all, string id, SwapStatus status)
    {
        var stored = all.FirstOrDefault(r => r.Id == id)
                     ?? throw new InvalidOperationException(Localizer.Instance["Swap_ErrorStale"]);
        if (stored.Status != SwapStatus.Pending)
            throw new InvalidOperationException(Localizer.Instance["Swap_ErrorNotPending"]);
        stored.Status = status;
        stored.RespondedAt = DateTime.Now;
    }

    public static List<Notification> ForUser(IEnumerable<Notification> all, string userId)
        => all.Where(n => n.UserId == userId).ToList();

    /// <summary>Markiert eigene als gelesen. Gibt zurück, ob sich etwas geändert hat (sonst kein Speichern nötig).</summary>
    public static bool MarkRead(IEnumerable<Notification> all, string userId, IReadOnlyCollection<string>? ids)
    {
        var changed = false;
        foreach (var n in all.Where(n => n.UserId == userId && !n.IsRead
                                         && (ids is null || ids.Count == 0 || ids.Contains(n.Id))))
        {
            n.IsRead = true;
            changed = true;
        }
        return changed;
    }
}
