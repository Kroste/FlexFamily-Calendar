namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Plattform-spezifischer Self-Replace: entpackt das frisch heruntergeladene Asset, ersetzt
/// die laufende Installation in einem Helper-Prozess und startet die neue Version. Der
/// Aufrufer (UI) wartet auf <see cref="InstallAndRestartAsync"/> und beendet die App danach
/// sofort, damit der Helper die alten Dateien überschreiben kann.
/// </summary>
public interface IUpdateInstaller
{
    /// <param name="downloadedAsset">Heruntergeladenes Asset im temp-Ordner.</param>
    /// <param name="ct">Abbruch-Token (wirkt nur bis zum Helper-Spawn).</param>
    Task InstallAndRestartAsync(string downloadedAsset, CancellationToken ct);
}

public static class UpdateInstallerFactory
{
    public static IUpdateInstaller? ForPlatform(UpdatePlatform platform) => platform switch
    {
        UpdatePlatform.LinuxTar => new LinuxTarInstaller(),
        UpdatePlatform.LinuxAppImage => new LinuxAppImageInstaller(),
        UpdatePlatform.WindowsZip => new WindowsZipInstaller(),
        _ => null
    };
}
