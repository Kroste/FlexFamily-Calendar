using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.Update;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// About-/InfoBox — App-Name, Version (aus Assembly), Kurzbeschreibung, GitHub-Link und
/// „Buy me a coffee"-Button (Kanon-Anforderung Master-CLAUDE.md).
/// </summary>
public partial class InfoViewModel : ObservableObject
{
    public string AppName => "FlexFamily Calendar";
    public string AppVersion => UpdateService.CurrentVersion();
    public string Description => "Familienplaner für Arbeitszeiten, Schichten, Aktivitäten (Schule/Kita/Sport), Krankmeldungen und Schichttausch.";
    public string GitHubUrl => "https://github.com/Kroste/FlexFamily-Calendar";
    public string CoffeeUrl => "https://buymeacoffee.com/kroste";

    public event Action? CloseRequested;

    [RelayCommand]
    private void OpenGitHub() => OpenUrl(GitHubUrl);

    [RelayCommand]
    private void OpenCoffee() => OpenUrl(CoffeeUrl);

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) { LogService.Warn("Browser-Öffnen schlug fehl: {0}", ex.Message); }
    }
}
