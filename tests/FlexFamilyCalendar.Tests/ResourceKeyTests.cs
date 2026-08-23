using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Styles und Resource-Keys scheitern in Avalonia STILL: weder ein toter
/// <c>{DynamicResource XyzBrush}</c> noch eine tote Style-Klasse erzeugt einen
/// Compile-Fehler — das Element rendert einfach falsch, und das fällt oft erst Releases
/// später auf. Diese Tests machen aus dem stillen Fehler einen roten Testlauf.
/// </summary>
public class ResourceKeyTests
{
    // Vom Fluent-Theme bereitgestellte Keys tragen alle das System-Präfix. Alles ohne
    // dieses Präfix gehört uns und muss im Repo definiert sein.
    private const string FrameworkPrefix = "System";

    private static readonly Regex ReferenceRx =
        new(@"\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_.]+)\}", RegexOptions.Compiled);
    private static readonly Regex DefinitionRx =
        new(@"x:Key=""([A-Za-z0-9_.]+)""", RegexOptions.Compiled);
    private static readonly Regex ColorLiteralRx =
        new(@"[A-Za-z.]+=""(#[0-9A-Fa-f]{6,8})""", RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FlexFamilyCalendar.slnx")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static List<string> XamlFiles() =>
        [.. Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.axaml", SearchOption.AllDirectories)];

    [Fact]
    public void Es_gibt_ueberhaupt_XAML_zu_pruefen()
    {
        // Schutz vor einem stillen Pass, wenn die Pfadauflösung mal danebengreift.
        Assert.True(XamlFiles().Count > 20);
    }

    [Fact]
    public void Jeder_referenzierte_Resource_Key_ist_auch_definiert()
    {
        var files = XamlFiles();

        var defined = files
            .SelectMany(f => DefinitionRx.Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        var dangling = new List<string>();
        foreach (var file in files)
        {
            foreach (Match m in ReferenceRx.Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[1].Value;
                if (key.StartsWith(FrameworkPrefix, StringComparison.Ordinal)) continue;
                if (defined.Contains(key)) continue;
                dangling.Add($"{Path.GetFileName(file)}: {key}");
            }
        }

        Assert.True(dangling.Count == 0,
            "Tote Resource-Keys (rendern still falsch):\n  " + string.Join("\n  ", dangling.Distinct()));
    }

    [Fact]
    public void Views_enthalten_keine_hartkodierten_Farbliterale()
    {
        // Alle Farben gehören in Styles/Palette.axaml. Vorher lagen 89 Literale in 21 Views,
        // dieselbe Bedeutung mit leicht unterschiedlichen Tönen.
        var offenders = new List<string>();
        foreach (var file in XamlFiles())
        {
            if (Path.GetFileName(file) == "Palette.axaml") continue;

            foreach (Match m in ColorLiteralRx.Matches(File.ReadAllText(file)))
                offenders.Add($"{Path.GetFileName(file)}: {m.Groups[1].Value}");
        }

        Assert.True(offenders.Count == 0,
            "Farbliterale außerhalb der Palette:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void Palette_definiert_in_keinem_Dictionary_einen_Key_doppelt()
    {
        // Pro Dictionary geprüft, nicht über die ganze Datei: Light und Dark MÜSSEN dieselben
        // Rollen-Namen tragen, das ist der Sinn von ThemeDictionaries. Ein doppelter Key
        // innerhalb eines Dictionaries wirft dagegen erst zur Laufzeit beim Laden.
        foreach (var (name, dict) in PaletteDictionaries())
        {
            var keys = dict.Elements()
                .Select(e => e.Attribute(X + "Key")?.Value)
                .OfType<string>()
                .ToList();

            var duplicates = keys
                .GroupBy(v => v, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.True(duplicates.Count == 0,
                $"Doppelte Keys in '{name}': {string.Join(", ", duplicates)}");
            Assert.NotEmpty(keys);
        }
    }

    [Fact]
    public void Hell_und_Dunkel_definieren_dieselben_Rollen()
    {
        // Eine Rolle, die nur in einem der beiden Themes existiert, fällt im anderen still aus:
        // die Bindung findet den Key nicht und das Element rendert mit Default-Farbe. Genau die
        // Art Fehler, die man erst bemerkt, wenn jemand das Theme umschaltet.
        var byName = PaletteDictionaries().ToDictionary(d => d.Name, d => d.Element);

        Assert.True(byName.ContainsKey("Light"), "Light-ThemeDictionary fehlt");
        Assert.True(byName.ContainsKey("Dark"), "Dark-ThemeDictionary fehlt");

        var light = KeysOf(byName["Light"]);
        var dark = KeysOf(byName["Dark"]);

        Assert.True(light.SetEquals(dark),
            "Nur in Light: " + string.Join(", ", light.Except(dark)) +
            " | Nur in Dark: " + string.Join(", ", dark.Except(light)));
        Assert.NotEmpty(light);
    }

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static HashSet<string> KeysOf(XElement dict) =>
    [
        .. dict.Elements()
            .Select(e => e.Attribute(X + "Key")?.Value)
            .OfType<string>()
    ];

    /// <summary>Alle ResourceDictionaries der Palette: das Wurzel-Dictionary plus die Theme-Varianten.</summary>
    private static List<(string Name, XElement Element)> PaletteDictionaries()
    {
        var doc = XDocument.Load(Path.Combine(RepoRoot(), "src", "Styles", "Palette.axaml"));
        var result = new List<(string, XElement)> { ("root", doc.Root!) };

        result.AddRange(doc.Descendants()
            .Where(e => e.Name.LocalName == "ResourceDictionary" && e.Attribute(X + "Key") is not null)
            .Select(e => (e.Attribute(X + "Key")!.Value, e)));

        return result;
    }
}
