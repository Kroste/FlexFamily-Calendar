using FlexFamilyCalendar.Controls;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Sicherstellen, dass die Parse-Logik des TimeEntryBox alle im Alltag sinnvollen Formate
/// akzeptiert und Müll ablehnt — sonst könnten falsche Uhrzeiten still übernommen werden.
/// </summary>
public class TimeEntryBoxParseTests
{
    [Theory]
    [InlineData("", 0, 0, true, true)]           // leer = null (ok)
    [InlineData("   ", 0, 0, true, true)]         // Whitespace = null
    [InlineData("8", 8, 0, true, false)]
    [InlineData("08", 8, 0, true, false)]
    [InlineData("23", 23, 0, true, false)]
    [InlineData("24", 0, 0, false, false)]        // 24 unzulässig
    [InlineData("830", 8, 30, true, false)]
    [InlineData("0830", 8, 30, true, false)]
    [InlineData("2359", 23, 59, true, false)]
    [InlineData("2400", 0, 0, false, false)]      // 24:00 unzulässig
    [InlineData("8:30", 8, 30, true, false)]
    [InlineData("08:30", 8, 30, true, false)]
    [InlineData("8:5", 8, 5, true, false)]
    [InlineData("08:5", 8, 5, true, false)]
    [InlineData("23:59", 23, 59, true, false)]
    [InlineData("25:00", 0, 0, false, false)]     // 25h unzulässig
    [InlineData("8:60", 0, 0, false, false)]      // 60min unzulässig
    [InlineData("abc", 0, 0, false, false)]
    [InlineData("8h", 0, 0, false, false)]
    [InlineData("8:", 0, 0, false, false)]        // unvollständig
    [InlineData("12345", 0, 0, false, false)]     // zu lang
    public void TryParse_Cases(string input, int expH, int expM, bool ok, bool expectNull)
    {
        var success = TimeEntryBox.TryParse(input, out var result);
        Assert.Equal(ok, success);
        if (!success) return;
        if (expectNull)
            Assert.Null(result);
        else
        {
            Assert.NotNull(result);
            Assert.Equal(expH, result!.Value.Hours);
            Assert.Equal(expM, result.Value.Minutes);
        }
    }
}
