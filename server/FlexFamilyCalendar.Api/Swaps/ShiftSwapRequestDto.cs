using FlexFamilyCalendar.Api.Models;

namespace FlexFamilyCalendar.Api.Swaps;

/// <summary>Schichttausch-Vorschlag, wie der Server ihn ausliefert.</summary>
public record ShiftSwapRequestDto(
    Guid Id,
    string CreatedAt,
    string? RespondedAt,
    int Status,
    int Mode,
    string FromUserId,
    string FromUserName,
    string FromDate,
    string FromEntryId,
    string ToUserId,
    string ToUserName,
    string? ToDate,
    string? ToEntryId,
    string Message)
{
    public static ShiftSwapRequestDto From(ShiftSwapRequestEntity e) => new(
        e.Id, e.CreatedAt, e.RespondedAt, e.Status, e.Mode,
        e.FromUserId, e.FromUserName, e.FromDate, e.FromEntryId,
        e.ToUserId, e.ToUserName, e.ToDate, e.ToEntryId, e.Message);
}
