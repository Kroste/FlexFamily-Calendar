using System.Globalization;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt zwischen Server-DTO und Desktop-<see cref="Notification"/> (CreatedAt als ISO-String).</summary>
public static class NotificationMapping
{
    public static Notification ToDesktop(ServerNotificationDto d) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        CreatedAt = ParseDate(d.CreatedAt) ?? DateTime.Now,
        IsRead = d.IsRead,
        MessageKey = d.MessageKey ?? "",
        Args = d.Args ?? new(),
        RelatedDate = d.RelatedDate,
        Action = d.Action,
        RelatedUserId = d.RelatedUserId
    };

    /// <summary>Nur was der Absender bestimmt — Id, Zeitstempel und Gelesen-Status setzt der Server.</summary>
    public static ServerCreateNotificationBody ToCreateBody(Notification n) => new(
        n.UserId, n.MessageKey, n.Args ?? new(), n.RelatedDate, n.Action, n.RelatedUserId);

    /// <summary>Der Server stempelt in UTC (…Z); angezeigt wird Ortszeit. Ältere, vom Client
    /// geschriebene Werte tragen ihren Offset selbst und bleiben unverändert.</summary>
    private static DateTime? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var d)
            ? (d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d)
            : null;
}
