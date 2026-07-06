using FlexFamilyCalendar.Api.Settings;

namespace FlexFamilyCalendar.Api.Tests;

// Regeln für PUT /api/settings — validiert und normalisiert HolidayState und
// OvernightHoursPerDay. Wichtig: keine leeren Bundesland-Codes (dann würde HolidayCalculator
// clientseitig auf Bayern default'en, was den Admin-Wunsch still ignoriert).
public class ServerSettingsRulesTests
{
    [Fact]
    public void Validate_leeres_Bundesland_liefert_Fehler()
    {
        var err = ServerSettingsRules.Validate(new ServerSettingsDto("", 2.0));
        Assert.NotNull(err);
        Assert.Contains("HolidayState", err);
    }

    [Fact]
    public void Validate_nur_Whitespace_Bundesland_liefert_Fehler()
    {
        var err = ServerSettingsRules.Validate(new ServerSettingsDto("   ", 2.0));
        Assert.NotNull(err);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(24.1)]
    [InlineData(100)]
    public void Validate_Übernachtungs_Stunden_außer_Bereich_liefert_Fehler(double hours)
    {
        var err = ServerSettingsRules.Validate(new ServerSettingsDto("BY", hours));
        Assert.NotNull(err);
        Assert.Contains("OvernightHoursPerDay", err);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(24)]
    public void Validate_gültige_Eingaben_liefern_null(double hours)
    {
        var err = ServerSettingsRules.Validate(new ServerSettingsDto("BY", hours));
        Assert.Null(err);
    }

    [Theory]
    [InlineData("by", "BY")]
    [InlineData("nw", "NW")]
    [InlineData("  hh  ", "HH")]
    [InlineData("BY", "BY")]
    public void NormalizeState_trim_und_upper(string input, string expected)
        => Assert.Equal(expected, ServerSettingsRules.NormalizeState(input));
}
