using System.Diagnostics;
using System.IO.Compression;

namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Windows-Zip-Update: Windows lässt das laufende .exe nicht ersetzen. Trick: PowerShell-Helper
/// startet detached, wartet bis die App sicher tot ist, benennt das alte .exe in *.old um (das
/// darf parallel laufen) und kopiert die neuen Dateien drüber. Anschließend Start der neuen App.
/// </summary>
public class WindowsZipInstaller : IUpdateInstaller
{
    public async Task InstallAndRestartAsync(string zipPath, CancellationToken ct)
    {
        var extractDir = Path.Combine(Path.GetTempPath(), $"ffc-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var currentBinary = Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Aktuelles Binary nicht ermittelbar.");
        var installDir = Path.GetDirectoryName(currentBinary)
            ?? throw new InvalidOperationException("Install-Ordner nicht ermittelbar.");
        var binaryName = Path.GetFileName(currentBinary);

        var script = Path.Combine(Path.GetTempPath(), $"ffc-update-{Guid.NewGuid():N}.ps1");
        var body = $$"""
            Start-Sleep -Seconds 2
            $install = "{{installDir.Replace("\\", "\\\\")}}"
            $newDir  = "{{extractDir.Replace("\\", "\\\\")}}"
            $binary  = "{{binaryName}}"
            $old     = Join-Path $install $binary
            try {
                # .old aus vorigem Update aufräumen
                Get-ChildItem -Path $install -Filter '*.old' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                # laufendes exe in *.old verschieben — Windows duldet das, weil der parent jetzt tot ist
                if (Test-Path $old) { Move-Item -Force $old "$old.old" }
                # neue Dateien drüberkopieren
                Copy-Item -Recurse -Force (Join-Path $newDir '*') $install
                Remove-Item -Recurse -Force $newDir
                Remove-Item -Force "{{zipPath.Replace("\\", "\\\\")}}"
                Start-Process -FilePath (Join-Path $install $binary)
            } catch {
                # Wenn etwas schiefläuft, läuft beim nächsten Start die alte Version weiter.
                "$($_.Exception.Message)" | Out-File "$env:TEMP\ffc-update-error.log"
            }
            """;
        await File.WriteAllTextAsync(script, body, ct);

        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"")
        {
            UseShellExecute = true,        // detached über Shell, damit der Helper den parent-Tod überlebt
            CreateNoWindow = true
        };
        Process.Start(psi);
    }
}
