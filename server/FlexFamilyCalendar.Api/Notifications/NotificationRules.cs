namespace FlexFamilyCalendar.Api.Notifications;

/// <summary>Eine neue Benachrichtigung, wie der Client sie anlegt. Id, Zeitstempel und Gelesen-Status setzt der Server.</summary>
public record CreateNotificationRequest(
    string UserId,
    string MessageKey,
    List<string>? Args,
    string? RelatedDate,
    string? Action,
    string? RelatedUserId);

/// <summary>Als gelesen markieren. <c>Ids</c> leer oder null = alle eigenen.</summary>
public record MarkNotificationsReadRequest(List<Guid>? Ids);

/// <summary>
/// Wer wem welche Benachrichtigung schicken darf.
///
/// Vorher gab es nur „ganze Liste lesen" und „ganze Liste ersetzen", beides für jeden
/// Angemeldeten. Damit las jeder alle Benachrichtigungen aller Nutzer — darunter
/// „X hat sich für den … krank gemeldet", also genau den Grund, den die Maskierung als
/// „Abwesend" verbirgt — und jeder Client konnte die ganze Tabelle löschen.
///
/// Anlegen für ANDERE bleibt erlaubt, das ist der Zweck der Sache („dir wurde eine Schicht
/// zugewiesen", „X bietet dir einen Tausch an"). Nicht-Admins dürfen aber nur die Nachrichten
/// schicken, die ihre eigenen Abläufe erzeugen — sonst könnte jeder Mitarbeiter den Kollegen
/// „Deine Schicht wurde entfernt" vortäuschen.
/// </summary>
public static class NotificationRules
{
    public const int MaxPerRequest = 100;
    public const int MaxArgs = 10;
    public const int MaxArgLength = 300;

    /// <summary>Was ein Nicht-Admin auslösen kann: Schichttausch in beide Richtungen und die eigene Krankmeldung.</summary>
    public static readonly IReadOnlySet<string> KeysForEveryone = new HashSet<string>(StringComparer.Ordinal)
    {
        "Notif_SwapOffered",
        "Notif_SwapAccepted",
        "Notif_SwapRejected",
        "Notif_SwapWithdrawn",
        "Notif_SickReported"
    };

    /// <summary>Die einzige Aktion, die eine Benachrichtigung tragen darf, und zu welchem Schlüssel sie gehört.</summary>
    public const string ReplanSickAction = "ReplanSick";

    /// <summary>Prüft eine einzelne neue Benachrichtigung. Gibt eine Fehlermeldung zurück, sonst null.</summary>
    public static string? CheckCreate(CreateNotificationRequest req, bool isAdmin)
    {
        if (string.IsNullOrWhiteSpace(req.UserId)) return "Empfänger fehlt.";
        if (string.IsNullOrWhiteSpace(req.MessageKey) || !req.MessageKey.StartsWith("Notif_", StringComparison.Ordinal))
            return "Unbekannter Nachrichtentyp.";
        if (!isAdmin && !KeysForEveryone.Contains(req.MessageKey))
            return $"Nachrichtentyp '{req.MessageKey}' ist Admin-Sache.";
        if (req.Action is not null
            && !(req.Action == ReplanSickAction && req.MessageKey == "Notif_SickReported"))
            return "Unbekannte Aktion.";
        if (req.Args is { Count: > MaxArgs }) return "Zu viele Argumente.";
        if (req.Args?.Any(a => a is not null && a.Length > MaxArgLength) == true) return "Argument zu lang.";
        return null;
    }

    /// <summary>Server-Nutzer-Ids sind Guids; Clients schreiben sie klein. Einheitlich normalisieren,
    /// damit der Empfänger-Filter beim Lesen nicht an der Schreibweise scheitert.</summary>
    public static string NormalizeUserId(string id) => id.Trim().ToLowerInvariant();
}
