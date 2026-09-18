using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class ShiftSwapMappingTests
{
    [Fact]
    public void ToDesktop_maps_enums_and_fields()
    {
        var dto = new ServerSwapRequestDto(
            "s1", "2026-05-29T10:00:00.0000000", null,
            (int)SwapStatus.Accepted, (int)SwapMode.Exchange,
            "u1", "Rike", "2026-06-01", "e1",
            "u2", "Anna", "2026-06-02", "e2", "Tauschst du?");

        var r = ShiftSwapMapping.ToDesktop(dto);

        Assert.Equal("s1", r.Id);
        Assert.Equal(SwapStatus.Accepted, r.Status);
        Assert.Equal(SwapMode.Exchange, r.Mode);
        Assert.Equal("u1", r.FromUserId);
        Assert.Equal("2026-06-02", r.ToDate);
        Assert.Equal("e2", r.ToEntryId);
        Assert.Equal("Tauschst du?", r.Message);
        Assert.Null(r.RespondedAt);
    }

    [Fact]
    public void Create_body_carries_the_intent_not_names_or_dates()
    {
        var giveAway = new ShiftSwapRequest
        {
            Mode = SwapMode.GiveAway,
            FromUserId = "u1", FromUserName = "Rike", FromDate = "2026-06-03", FromEntryId = "e3",
            ToUserId = "u2", ToUserName = "Anna", Message = ""
        };

        var body = ShiftSwapMapping.ToCreateBody(giveAway);

        Assert.Equal((int)SwapMode.GiveAway, body.Mode);
        Assert.Equal("u1", body.FromUserId);
        Assert.Equal("e3", body.FromEntryId);
        Assert.Equal("u2", body.ToUserId);
        Assert.Null(body.ToEntryId);
        Assert.Null(body.Message);           // leer = nicht mitschicken
    }

    [Fact]
    public void Server_response_keeps_status_mode_and_converts_utc()
    {
        var created = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        var dto = new ServerSwapRequestDto("s3", created.ToString("o"), null,
            (int)SwapStatus.Rejected, (int)SwapMode.Exchange,
            "u1", "A", "2026-06-01", "e1", "u2", "B", "2026-06-02", "e2", "x");

        var back = ShiftSwapMapping.ToDesktop(dto);

        Assert.Equal(SwapStatus.Rejected, back.Status);
        Assert.Equal(SwapMode.Exchange, back.Mode);
        Assert.Equal(created.ToLocalTime(), back.CreatedAt);
        Assert.Null(back.RespondedAt);
    }
}
