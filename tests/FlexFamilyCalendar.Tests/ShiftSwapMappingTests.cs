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
    public void Giveaway_without_counter_shift_keeps_nulls()
    {
        var r = new ShiftSwapRequest
        {
            Id = "s2",
            Status = SwapStatus.Pending,
            Mode = SwapMode.GiveAway,
            FromUserId = "u1", FromUserName = "Rike", FromDate = "2026-06-03", FromEntryId = "e3",
            ToUserId = "u2", ToUserName = "Anna",
            ToDate = null, ToEntryId = null,
            Message = ""
        };

        var dto = ShiftSwapMapping.ToServer(r);

        Assert.Equal((int)SwapStatus.Pending, dto.Status);
        Assert.Equal((int)SwapMode.GiveAway, dto.Mode);
        Assert.Null(dto.ToDate);
        Assert.Null(dto.ToEntryId);
    }

    [Fact]
    public void Round_trip_preserves_status_mode_and_timestamps()
    {
        var created = new DateTime(2026, 5, 29, 12, 0, 0);
        var responded = new DateTime(2026, 5, 29, 13, 30, 0);
        var r = new ShiftSwapRequest
        {
            Id = "s3", CreatedAt = created, RespondedAt = responded,
            Status = SwapStatus.Rejected, Mode = SwapMode.Exchange,
            FromUserId = "u1", FromUserName = "A", FromDate = "2026-06-01", FromEntryId = "e1",
            ToUserId = "u2", ToUserName = "B", ToDate = "2026-06-02", ToEntryId = "e2",
            Message = "x"
        };

        var back = ShiftSwapMapping.ToDesktop(ShiftSwapMapping.ToServer(r));

        Assert.Equal(SwapStatus.Rejected, back.Status);
        Assert.Equal(SwapMode.Exchange, back.Mode);
        Assert.Equal(created, back.CreatedAt);
        Assert.Equal(responded, back.RespondedAt);
    }
}
