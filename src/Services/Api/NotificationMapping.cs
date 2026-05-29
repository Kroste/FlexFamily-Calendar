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

    public static ServerNotificationDto ToServer(Notification n) => new(
        n.Id, n.UserId,
        n.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
        n.IsRead, n.MessageKey,
        n.Args ?? new(), n.RelatedDate, n.Action, n.RelatedUserId);

    private static DateTime? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var d) ? d : null;
}
