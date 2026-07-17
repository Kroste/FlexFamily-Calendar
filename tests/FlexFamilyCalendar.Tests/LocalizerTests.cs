using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using FlexFamilyCalendar.Localization;
using Xunit;

namespace FlexFamilyCalendar.Tests;

public class LocalizerTests
{
    [Fact]
    public void German_Is_Default_Lookup()
    {
        Localizer.Instance.SetLanguage("de");
        Assert.Equal("Anmelden", Localizer.Instance["Login_SignIn"]);
    }

    [Fact]
    public void English_Lookup_Works()
    {
        Localizer.Instance.SetLanguage("en");
        Assert.Equal("Sign in", Localizer.Instance["Login_SignIn"]);
        Localizer.Instance.SetLanguage("de");
    }

    [Fact]
    public void MissingKey_Returns_KeyItself()
    {
        Localizer.Instance.SetLanguage("de");
        Assert.Equal("___does_not_exist___", Localizer.Instance["___does_not_exist___"]);
    }

    [Fact]
    public void SetLanguage_Raises_LanguageChanged()
    {
        Localizer.Instance.SetLanguage("de");
        var fired = false;
        void Handler(object? s, EventArgs e) => fired = true;
        Localizer.Instance.LanguageChanged += Handler;
        try { Localizer.Instance.SetLanguage("en"); }
        finally { Localizer.Instance.LanguageChanged -= Handler; Localizer.Instance.SetLanguage("de"); }
        Assert.True(fired);
    }

    [Fact]
    public void SetLanguage_Raises_IndexerPropertyChanged()
    {
        string? changed = null;
        void Handler(object? s, PropertyChangedEventArgs e) => changed = e.PropertyName;
        Localizer.Instance.PropertyChanged += Handler;
        try { Localizer.Instance.SetLanguage("en"); }
        finally { Localizer.Instance.PropertyChanged -= Handler; Localizer.Instance.SetLanguage("de"); }
        Assert.Equal("Item[]", changed);
    }

    [Fact]
    public void UnknownLanguage_FallsBackToBase()
    {
        Localizer.Instance.SetLanguage("xx");
        Assert.Equal("de", Localizer.Instance.CurrentLanguage);
    }

    [Fact]
    public void De_And_En_Have_Identical_Key_Sets()
    {
        var de = LoadKeys("de");
        var en = LoadKeys("en");
        var missingInEn = de.Except(en).ToList();
        var missingInDe = en.Except(de).ToList();
        Assert.True(missingInEn.Count == 0, "Fehlend in en.json: " + string.Join(", ", missingInEn));
        Assert.True(missingInDe.Count == 0, "Fehlend in de.json: " + string.Join(", ", missingInDe));
    }

    private static HashSet<string> LoadKeys(string code)
    {
        var asm = typeof(Localizer).Assembly;
        var name = asm.GetManifestResourceNames()
            .First(n => n.EndsWith($"i18n.{code}.json", StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())!;
        return dict.Keys.ToHashSet();
    }
}
