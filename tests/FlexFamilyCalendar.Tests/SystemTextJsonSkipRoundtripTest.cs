using FlexFamilyCalendar.Services.Api;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class SystemTextJsonSkipRoundtripTest
{
    [Fact]
    public void ServerDto_Skips_Roundtrip_Via_SystemTextJson()
    {
        var skip = new ServerRecurrenceSkipDto("s1", new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 14), "Urlaub");
        var dto = new ServerRecurringActivityDto(
            "r1", "u1", "Mia", "Fußball", "cat1",
            new TimeOnly(16, 0), new TimeOnly(17, 0),
            new List<int> { 4 }, SkipOnHolidays: false,
            Skips: new List<ServerRecurrenceSkipDto> { skip });

        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        System.Console.WriteLine(json);
        var back = System.Text.Json.JsonSerializer.Deserialize<ServerRecurringActivityDto>(json)!;

        Assert.NotNull(back.Skips);
        Assert.Single(back.Skips!);
        Assert.Equal("Urlaub", back.Skips![0].Reason);
        Assert.Equal(new DateOnly(2026, 7, 14), back.Skips[0].To);
    }
}
