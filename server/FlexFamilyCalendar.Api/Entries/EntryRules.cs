using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Entries;

/// <summary>Entscheidet serverseitig, was ein Anfragender von einem Eintrag sehen darf.</summary>
public static class EntryVisibility
{
    /// <summary>
    /// Projiziert einen Eintrag für den Anfragenden – oder null, wenn er ihn nicht sehen darf.
    /// <paramref name="isDayFinalized"/> signalisiert, dass der Admin den Tag als „Planung fertig"
    /// markiert hat — vorher bleiben Schichten für Fremde verborgen, damit halbfertige
    /// Wochenplanungen nicht als Fakten missverstanden werden.
    /// </summary>
    public static EntryDto? Project(CalendarEntry e, Guid requesterId, bool isAdmin, bool isDayFinalized)
    {
        var isOwner = e.UserId == requesterId;

        // Admin sieht alles — er macht die Planung.
        if (isAdmin) return EntryDto.Full(e);

        // Für Nicht-Admins: ALLE Einträge anderer Personen (unabhängig vom Typ) sind erst
        // sichtbar, wenn der Admin den Tag freigegeben hat. Vorher sind Schichten, Aktivitäten
        // der Kinder etc. Planungsentwürfe, die sich noch ändern können.
        if (!isOwner)
        {
            if (!isDayFinalized) return null;
            if (e.Status != EntryStatus.Approved) return null;   // Pending/Rejected weiter nur Owner+Admin
            return EntryTypes.IsPrivate(e.Type) ? EntryDto.Mask(e) : EntryDto.Full(e);
        }

        // Eigentümer: eigene Work-Schichten kommen vom Admin — auch die erst nach Freigabe.
        if (e.Type == EntryTypes.Work && !isDayFinalized) return null;

        // Alle anderen eigenen Einträge (Krank-/Urlaubswunsch, Aktivität, Übernachtung)
        // sind immer sichtbar — auch Pending, damit der User seinen offenen Wunsch sieht.
        return EntryDto.Full(e);
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
        // Nicht-Admin darf für sich anlegen: Urlaubswunsch, Krankmeldung ODER einen freien
        // Custom-Zeitblock (persönlicher Termin) — Custom zählt nicht zur Arbeitszeit.
        if (type is not (EntryTypes.Vacation or EntryTypes.SickLeave or EntryTypes.Custom))
            return "Als Nicht-Admin sind nur Urlaubswunsch, Krankmeldung oder ein freier Termin möglich.";
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
        TimeOnly? start, TimeOnly? end, string? categoryLabel, string? activityTypeId = null)
    {
        if (endDate is { } ed && ed < date)
            return "Enddatum liegt vor dem Startdatum.";
        if (EntryTypes.IsTimed(type) && (start is null || end is null))
            return "Schichten brauchen Start- und Endzeit.";
        // Bei Aktivitäten reicht ENTWEDER eine ActivityTypeId-Referenz (übliche Auswahl aus Admin-Kategorien)
        // ODER ein Freitext-Label (categoryLabel). Beide leer = ungültig.
        if (type == EntryTypes.Activity
            && string.IsNullOrWhiteSpace(categoryLabel)
            && string.IsNullOrWhiteSpace(activityTypeId))
            return "Aktivitäten brauchen eine Kategorie.";
        // Custom braucht immer einen Freitext-Titel, sonst ist der Eintrag nichtssagend.
        if (type == EntryTypes.Custom && string.IsNullOrWhiteSpace(categoryLabel))
            return "Freie Termine brauchen einen Titel.";
        return null;
    }
}
