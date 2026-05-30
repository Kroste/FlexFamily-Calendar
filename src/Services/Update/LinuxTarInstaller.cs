using System.Diagnostics;

namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Linux-Tar.Gz-Update: entpackt das Archive, schreibt ein Helper-Bash-Script in /tmp,
/// startet das Helper-Script im Hintergrund (nohup) und überlässt dem Aufrufer das Beenden
/// des Prozesses. Das Script wartet eine Sekunde (alte App ist dann sauber tot), spiegelt
/// die neuen Dateien in den Install-Ordner und startet die neue Binary.
/// </summary>
public class LinuxTarInstaller : IUpdateInstaller
{
    public async Task InstallAndRestartAsync(string tarGzPath, CancellationToken ct)
    {
        var extractDir = Path.Combine(Path.GetTempPath(), $"ffc-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractDir);

        await RunAsync("tar", $"-xzf \"{tarGzPath}\" -C \"{extractDir}\"", ct);

        var currentBinary = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Aktuelles Binary nicht ermittelbar.");
        var installDir = Path.GetDirectoryName(currentBinary)
            ?? throw new InvalidOperationException("Install-Ordner nicht ermittelbar.");
        var binaryName = Path.GetFileName(currentBinary);

        var script = Path.Combine(Path.GetTempPath(), $"ffc-update-{Guid.NewGuid():N}.sh");
        var body = $"""
            #!/bin/sh
            set -e
            sleep 1
            cp -fR "{extractDir}/." "{installDir}/"
            chmod +x "{installDir}/{binaryName}"
            rm -rf "{extractDir}" "{tarGzPath}"
            exec "{installDir}/{binaryName}" &
            """;
        await File.WriteAllTextAsync(script, body, ct);
        await RunAsync("chmod", $"+x \"{script}\"", ct);

        // Detached, damit das Script den parent-Tod überlebt.
        var psi = new ProcessStartInfo("/bin/sh", $"-c \"nohup '{script}' >/dev/null 2>&1 &\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }

    private static async Task RunAsync(string file, string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"{file} konnte nicht gestartet werden.");
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{file} {args} → Exit {p.ExitCode}: {await p.StandardError.ReadToEndAsync(ct)}");
    }
}
