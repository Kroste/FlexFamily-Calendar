using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using FlexFamilyCalendar.Localization;
using Xunit;

namespace FlexFamilyCalendar.Tests;

[Collection("Localizer")]
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
    public void SetLanguage_benachrichtigt_jeden_gebundenen_Wrapper()
    {
        // Ersetzt den alten Test auf PropertyChanged("Item[]"). Die WPF-Indexer-Konvention
        // verarbeitet Avalonia 12 nur unzuverlässig — Fenster ohne Fokus blieben stale.
        // Jetzt feuert jeder LocalizedString ein reguläres PropertyChanged(Value).
        var wrapper = LocalizedString.Get("Common_Close");
        var changed = new List<string?>();
        void Handler(object? s, PropertyChangedEventArgs e) => changed.Add(e.PropertyName);
        wrapper.PropertyChanged += Handler;

        try { Localizer.Instance.SetLanguage("en"); }
        finally { wrapper.PropertyChanged -= Handler; Localizer.Instance.SetLanguage("de"); }

        Assert.Contains(nameof(LocalizedString.Value), changed);
    }

    [Fact]
    public void Wrapper_liefert_pro_Schluessel_immer_dieselbe_Instanz()
    {
        // Der statische Cache ist die Lebensversicherung: Avalonia hält Binding.Source nicht
        // stark, ein pro Binding neu erzeugter Wrapper wäre nach dem ersten Rendering weg.
        Assert.Same(LocalizedString.Get("Common_Close"), LocalizedString.Get("Common_Close"));
    }

    [Fact]
    public void Wrapper_Wert_folgt_der_Sprache()
    {
        var wrapper = LocalizedString.Get("Common_Close");
        try
        {
            Localizer.Instance.SetLanguage("de");
            var de = wrapper.Value;
            Localizer.Instance.SetLanguage("en");
            var en = wrapper.Value;

            Assert.Equal(Localizer.Instance["Common_Close"], en);
            Assert.NotEqual(de, en);
        }
        finally { Localizer.Instance.SetLanguage("de"); }
    }

    [Fact]
    public void Mehrere_Wrapper_werden_alle_benachrichtigt()
    {
        // Der eigentliche Bug hinter dem Umbau: es aktualisierte sich nur das Fenster, das den
        // Wechsel ausgelöst hat. Mehrere Wrapper stehen hier für mehrere Fenster. Gezählt wird
        // pro Wrapper — der Localizer ist ein Singleton, ein absoluter Gesamtzähler wäre von
        // anderen Tests beeinflussbar.
        var a = LocalizedString.Get("Common_Close");
        var b = LocalizedString.Get("Common_Cancel");
        var hitsA = 0;
        var hitsB = 0;
        void HandlerA(object? s, PropertyChangedEventArgs e) => hitsA++;
        void HandlerB(object? s, PropertyChangedEventArgs e) => hitsB++;

        a.PropertyChanged += HandlerA;
        b.PropertyChanged += HandlerB;
        try { Localizer.Instance.SetLanguage("en"); }
        finally
        {
            a.PropertyChanged -= HandlerA;
            b.PropertyChanged -= HandlerB;
            Localizer.Instance.SetLanguage("de");
        }

        Assert.True(hitsA > 0, "Wrapper A wurde nicht benachrichtigt");
        Assert.True(hitsB > 0, "Wrapper B wurde nicht benachrichtigt");
    }

    [Fact]
    public void Ein_Sprachwechsel_benachrichtigt_jeden_Wrapper_genau_einmal()
    {
        // Doppelte Notifications wären kein Fehler, aber unnötige Re-Layouts bei gut 400
        // gebundenen Schlüsseln.
        var wrapper = LocalizedString.Get("Common_Save");
        var hits = 0;
        void Handler(object? s, PropertyChangedEventArgs e) => hits++;

        wrapper.PropertyChanged += Handler;
        try { Localizer.Instance.SetLanguage("en"); }
        finally { wrapper.PropertyChanged -= Handler; Localizer.Instance.SetLanguage("de"); }

        Assert.Equal(1, hits);
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
