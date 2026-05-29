using System.Globalization;
using FlexFamilyCalendar.Models;

namespace FlexFamilyCalendar.Services.Api;

/// <summary>Übersetzt zwischen Server-DTO und Desktop-<see cref="ShiftSwapRequest"/> (Zeitstempel als ISO-Strings).</summary>
public static class ShiftSwapMapping
{
    public static ShiftSwapRequest ToDesktop(ServerSwapRequestDto d) => new()
    {
        Id = d.Id,
        CreatedAt = ParseDate(d.CreatedAt) ?? DateTime.Now,
        RespondedAt = ParseDate(d.RespondedAt),
        Status = (SwapStatus)d.Status,
        Mode = (SwapMode)d.Mode,
        FromUserId = d.FromUserId,
        FromUserName = d.FromUserName,
        FromDate = d.FromDate,
        FromEntryId = d.FromEntryId,
        ToUserId = d.ToUserId,
        ToUserName = d.ToUserName,
        ToDate = d.ToDate,
        ToEntryId = d.ToEntryId,
        Message = d.Message ?? ""
    };

    public static ServerSwapRequestDto ToServer(ShiftSwapRequest a) => new(
        a.Id,
        a.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
        a.RespondedAt?.ToString("o", CultureInfo.InvariantCulture),
        (int)a.Status,
        (int)a.Mode,
        a.FromUserId, a.FromUserName, a.FromDate, a.FromEntryId,
        a.ToUserId, a.ToUserName, a.ToDate, a.ToEntryId, a.Message ?? "");

    private static DateTime? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var d) ? d : null;
}
