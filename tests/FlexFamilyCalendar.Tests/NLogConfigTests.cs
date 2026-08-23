using System.Runtime.CompilerServices;
using FlexFamilyCalendar.Services;
using NLog;
using NLog.Targets;

namespace FlexFamilyCalendar.Tests;

// Die nlog.config wird zur Laufzeit geparst, nicht beim Kompilieren — ein veraltetes
// Attribut nach einem NLog-Major-Update fällt sonst erst beim ersten App-Start auf
// (throwConfigExceptions="true" lässt die App dann gar nicht hochkommen).
public class NLogConfigTests
{
    private static string ConfigPath =>
        Path.Combine(AppContext.BaseDirectory, "nlog.config");

    [Fact]
    public void Config_liegt_neben_der_Exe()
    {
        // CopyToOutputDirectory in der csproj — ohne die Datei fällt NLog auf "gar kein Log" zurück.
        Assert.True(File.Exists(ConfigPath), $"nlog.config fehlt unter {ConfigPath}");
    }

    [Fact]
    public void Config_laedt_ohne_Fehler()
    {
        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        Assert.NotNull(factory.Configuration);
        Assert.Contains(factory.Configuration!.AllTargets, t => t is FileTarget);
        Assert.Contains(factory.Configuration.AllTargets, t => t is ConsoleTarget);
    }

    [Fact]
    public void Datei_Target_loggt_ab_Trace()
    {
        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        var fileRule = factory.Configuration!.LoggingRules
            .Single(r => r.Targets.Any(t => t is FileTarget));
        Assert.Contains(LogLevel.Trace, fileRule.Levels);
    }

    [Fact]
    public void Jedes_Target_maskiert_Secrets_tatsaechlich()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(MaskingLayoutRenderer).Module.ModuleHandle);

        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        // Gerendert statt nach "masked" im Layout-String gesucht: ein ${var:…}-Umweg
        // sieht in der Config richtig aus, wird von NLog aber nicht zwingend erneut als
        // Layout interpretiert — die Maskierung fiele dann still aus.
        var evt = new LogEventInfo(LogLevel.Info, "test",
            "Login {\"username\":\"lars\",\"password\":\"streng-geheim\"}");

        var targets = factory.Configuration!.AllTargets.OfType<TargetWithLayout>().ToList();
        Assert.NotEmpty(targets);
        foreach (var target in targets)
        {
            var rendered = target.Layout.Render(evt);
            Assert.DoesNotContain("streng-geheim", rendered);
            Assert.Contains("***", rendered);
        }
    }
}
