namespace FlexFamilyCalendar.Services.Update;

/// <summary>Wie auf welche Plattform die App sich aktualisieren will.</summary>
public enum UpdatePlatform
{
    LinuxTar,     // self-contained linux-x64.tar.gz → Binary ersetzen
    LinuxAppImage,// x86_64.AppImage → AppImage-Datei ersetzen ($APPIMAGE-env)
    WindowsZip,   // win-x64.zip → .exe via rename-Trick ersetzen
    Unsupported   // macOS oder unbekannt
}

/// <summary>Was der UpdateService über ein verfügbares Update weiß.</summary>
public record UpdateInfo(
    string CurrentVersion,
    string LatestVersion,
    string ReleaseUrl,
    string ChangelogMarkdown,
    UpdateAsset? Asset);

public record UpdateAsset(
    string FileName,
    string DownloadUrl,
    long Size,
    UpdatePlatform Platform);
