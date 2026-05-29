using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Notifications;

/// <summary>Benachrichtigung für Lesen und Ersetzen (PUT).</summary>
public record NotificationDto(
    Guid Id,
    string UserId,
    string CreatedAt,
    bool IsRead,
    string MessageKey,
    List<string> Args,
    string? RelatedDate,
    string? Action,
    string? RelatedUserId)
{
    public static NotificationDto From(NotificationEntity e) => new(
        e.Id, e.UserId, e.CreatedAt, e.IsRead, e.MessageKey,
        e.Args, e.RelatedDate, e.Action, e.RelatedUserId);
}
