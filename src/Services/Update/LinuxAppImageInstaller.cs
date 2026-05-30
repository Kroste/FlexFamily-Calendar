using System.Diagnostics;

namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Linux-AppImage-Update: $APPIMAGE zeigt auf die laufende .AppImage-Datei. Helper-Script
/// wartet, ersetzt die Datei durch das frische Download, restartet sie.
/// </summary>
public class LinuxAppImageInstaller : IUpdateInstaller
{
    public async Task InstallAndRestartAsync(string newAppImage, CancellationToken ct)
    {
        var currentAppImage = Environment.GetEnvironmentVariable("APPIMAGE")
            ?? throw new InvalidOperationException("$APPIMAGE nicht gesetzt — läuft die App wirklich als AppImage?");

        var script = Path.Combine(Path.GetTempPath(), $"ffc-update-{Guid.NewGuid():N}.sh");
        var body = $"""
            #!/bin/sh
            set -e
            sleep 1
            mv -f "{newAppImage}" "{currentAppImage}"
            chmod +x "{currentAppImage}"
            exec "{currentAppImage}" &
            """;
        await File.WriteAllTextAsync(script, body, ct);

        var chmod = new ProcessStartInfo("chmod", $"+x \"{script}\"") { UseShellExecute = false, CreateNoWindow = true };
        (Process.Start(chmod) ?? throw new InvalidOperationException("chmod nicht gestartet.")).WaitForExit();

        var psi = new ProcessStartInfo("/bin/sh", $"-c \"nohup '{script}' >/dev/null 2>&1 &\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }
}
