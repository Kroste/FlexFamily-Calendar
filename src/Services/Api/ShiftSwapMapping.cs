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

    /// <summary>Nur die Absicht — Namen und Datum leitet der Server aus den Einträgen ab.</summary>
    public static ServerCreateSwapBody ToCreateBody(ShiftSwapRequest a) => new(
        (int)a.Mode,
        string.IsNullOrEmpty(a.FromUserId) ? null : a.FromUserId,
        a.FromEntryId,
        a.ToUserId,
        string.IsNullOrEmpty(a.ToEntryId) ? null : a.ToEntryId,
        string.IsNullOrWhiteSpace(a.Message) ? null : a.Message);

    /// <summary>Der Server stempelt in UTC (…Z); angezeigt wird Ortszeit. Ältere, vom Client
    /// geschriebene Werte tragen ihren Offset selbst und bleiben unverändert.</summary>
    private static DateTime? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateTime.TryParse(s, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var d)
            ? (d.Kind == DateTimeKind.Utc ? d.ToLocalTime() : d)
            : null;
}
