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
    public void Create_body_carries_only_what_the_sender_decides()
    {
        var n = new Notification
        {
            Id = "n2", UserId = "u1", IsRead = true,
            MessageKey = "Notif_SwapOffered", Args = new List<string> { "x" },
            RelatedDate = "2026-09-18", Action = null, RelatedUserId = null
        };

        var body = NotificationMapping.ToCreateBody(n);

        // Id, Zeitstempel und Gelesen-Status setzt der Server — ein Client kann weder eine
        // fremde Id überschreiben noch eine Nachricht als „schon gelesen" einschleusen.
        Assert.Equal("u1", body.UserId);
        Assert.Equal("Notif_SwapOffered", body.MessageKey);
        Assert.Equal(new[] { "x" }, body.Args);
        Assert.Equal("2026-09-18", body.RelatedDate);
        Assert.Null(body.Action);
    }

    [Fact]
    public void Server_utc_timestamp_is_shown_in_local_time()
    {
        var utc = new DateTime(2026, 9, 18, 8, 15, 0, DateTimeKind.Utc);
        var dto = new ServerNotificationDto("n3", "u9", utc.ToString("o"), true, "K", new(), null, null, null);

        var back = NotificationMapping.ToDesktop(dto);

        Assert.Equal(utc.ToLocalTime(), back.CreatedAt);
        Assert.True(back.IsRead);
    }

    [Fact]
    public void Older_client_timestamp_with_offset_stays_as_it_was()
    {
        var local = new DateTime(2026, 5, 29, 8, 15, 0, DateTimeKind.Local);
        var dto = new ServerNotificationDto("n4", "u9", local.ToString("o"), false, "K", new(), null, null, null);

        Assert.Equal(local, NotificationMapping.ToDesktop(dto).CreatedAt);
    }
}
