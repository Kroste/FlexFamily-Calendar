using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services.Api;

namespace FlexFamilyCalendar.Tests;

public class NotificationMappingTests
{
    [Fact]
    public void ToDesktop_maps_fields_and_args()
    {
        var dto = new ServerNotificationDto(
            "n1", "u1", "2026-05-29T09:00:00.0000000", true,
            "Notif_SickReported", new List<string> { "Anna", "2026-06-02" },
            "2026-06-02", "ReplanSick", "u2");

        var n = NotificationMapping.ToDesktop(dto);

        Assert.Equal("n1", n.Id);
        Assert.Equal("u1", n.UserId);
        Assert.True(n.IsRead);
        Assert.Equal("Notif_SickReported", n.MessageKey);
        Assert.Equal(new[] { "Anna", "2026-06-02" }, n.Args);
        Assert.Equal("2026-06-02", n.RelatedDate);
        Assert.Equal("ReplanSick", n.Action);
        Assert.Equal("u2", n.RelatedUserId);
    }

    [Fact]
    public void ToServer_serializes_args_and_keeps_nulls()
    {
        var n = new Notification
        {
            Id = "n2", UserId = "u1", IsRead = false,
            MessageKey = "Notif_Generic", Args = new List<string> { "x" },
            RelatedDate = null, Action = null, RelatedUserId = null
        };

        var dto = NotificationMapping.ToServer(n);

        Assert.Equal(new[] { "x" }, dto.Args);
        Assert.Null(dto.RelatedDate);
        Assert.Null(dto.Action);
        Assert.False(dto.IsRead);
    }

    [Fact]
    public void Round_trip_preserves_read_state_and_created_at()
    {
        var created = new DateTime(2026, 5, 29, 8, 15, 0);
        var n = new Notification
        {
            Id = "n3", UserId = "u9", CreatedAt = created, IsRead = true,
            MessageKey = "K", Args = new List<string>()
        };

        var back = NotificationMapping.ToDesktop(NotificationMapping.ToServer(n));

        Assert.Equal(created, back.CreatedAt);
        Assert.True(back.IsRead);
        Assert.Empty(back.Args);
    }
}
