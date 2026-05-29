using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Entries;

/// <summary>Entscheidet serverseitig, was ein Anfragender von einem Eintrag sehen darf.</summary>
public static class EntryVisibility
{
    /// <summary>Projiziert einen Eintrag für den Anfragenden – oder null, wenn er ihn nicht sehen darf.</summary>
    public static EntryDto? Project(CalendarEntry e, Guid requesterId, bool isAdmin)
    {
        // Admin und Eigentümer sehen alles.
        if (isAdmin || e.UserId == requesterId)
            return EntryDto.Full(e);

        // Fremde sehen nur genehmigte Einträge …
        if (e.Status != EntryStatus.Approved)
            return null;

        // … und private (Urlaub/Krank) nur maskiert als „Abwesend".
        return EntryTypes.IsPrivate(e.Type) ? EntryDto.Mask(e) : EntryDto.Full(e);
    }
}

/// <summary>Regelt serverseitig, wer welche Einträge anlegen/genehmigen darf.</summary>
public static class EntryWriteRules
{
    /// <summary>Darf der Anfragende diesen Eintrag anlegen? Gibt eine Fehlermeldung zurück, sonst null.</summary>
    public static string? CheckCreate(string type, Guid targetUserId, Guid requesterId, bool isAdmin)
    {
        if (!EntryTypes.IsKnown(type))
            return $"Unbekannter Typ '{type}'.";
        if (isAdmin)
            return null;
        if (targetUserId != requesterId)
            return "Du darfst nur eigene Einträge anlegen.";
        if (type is not (EntryTypes.Vacation or EntryTypes.SickLeave))
            return "Als Nicht-Admin sind nur Urlaubswunsch oder Krankmeldung möglich.";
        return null;
    }

    /// <summary>Startstatus: Urlaubswünsche von Nicht-Admins müssen genehmigt werden, sonst sofort gültig.</summary>
    public static string InitialStatus(string type, bool isAdmin)
    {
        if (isAdmin)
            return EntryStatus.Approved;
        return type == EntryTypes.Vacation ? EntryStatus.Pending : EntryStatus.Approved;
    }

    /// <summary>Inhaltliche Prüfung (Pflichtfelder je Typ). Gibt eine Fehlermeldung zurück, sonst null.</summary>
    public static string? Validate(string type, DateOnly date, DateOnly? endDate,
        TimeOnly? start, TimeOnly? end, string? categoryLabel)
    {
        if (endDate is { } ed && ed < date)
            return "Enddatum liegt vor dem Startdatum.";
        if (EntryTypes.IsTimed(type) && (start is null || end is null))
            return "Schichten brauchen Start- und Endzeit.";
        if (type == EntryTypes.Activity && string.IsNullOrWhiteSpace(categoryLabel))
            return "Aktivitäten brauchen eine Kategorie.";
        return null;
    }
}
