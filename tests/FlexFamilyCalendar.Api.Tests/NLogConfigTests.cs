using System.Runtime.CompilerServices;
using FlexFamilyCalendar.Api.Logging;
using NLog;
using NLog.Targets;

namespace FlexFamilyCalendar.Api.Tests;

// Gegenstück zum gleichnamigen Test im Client: die Server-Config wird ebenfalls erst zur
// Laufzeit geparst, und throwConfigExceptions="true" heißt, dass ein Fehler darin den
// Container gar nicht erst hochkommen lässt.
public class NLogConfigTests
{
    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "nlog.config");

    [Fact]
    public void Config_liegt_neben_der_Api_Assembly()
    {
        Assert.True(File.Exists(ConfigPath), $"nlog.config fehlt unter {ConfigPath}");
    }

    [Fact]
    public void Config_laedt_ohne_Fehler()
    {
        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        Assert.NotNull(factory.Configuration);
        // Die Konsole ist im Container das, was `docker logs` zeigt — sie darf nie wegfallen.
        Assert.Contains(factory.Configuration!.AllTargets, t => t is ConsoleTarget);
        Assert.Contains(factory.Configuration.AllTargets, t => t is FileTarget);
    }

    [Fact]
    public void Jedes_Target_maskiert_Secrets_tatsaechlich()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(MaskingLayoutRenderer).Module.ModuleHandle);

        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        var evt = new LogEventInfo(LogLevel.Info, "test",
            "Host=db;Username=ffc;Password=streng-geheim");

        var targets = factory.Configuration!.AllTargets.OfType<TargetWithLayout>().ToList();
        Assert.NotEmpty(targets);
        foreach (var target in targets)
        {
            var rendered = target.Layout.Render(evt);
            Assert.DoesNotContain("streng-geheim", rendered);
            Assert.Contains("Host=db", rendered);
        }
    }

    [Fact]
    public void Log_Verzeichnis_faellt_ohne_FFC_LOG_DIR_auf_Temp_zurueck()
    {
        // Ohne die ENV-Variable darf die Datei nicht in /var/log landen — lokale Läufe und
        // CI-Runner haben dort keine Schreibrechte, und createDirs="true" würde es versuchen.
        var factory = new LogFactory { ThrowConfigExceptions = true };
        factory.Setup().LoadConfigurationFromFile(ConfigPath, optional: false);

        var file = factory.Configuration!.AllTargets.OfType<FileTarget>().Single();
        var path = file.FileName.Render(LogEventInfo.CreateNullEvent());

        Assert.StartsWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar), path);
    }
}
