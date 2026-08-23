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
    private readonly UpdateCheckRunner? _runUpdateCheck;

    /// <param name="runUpdateCheck">
    /// Manuelle Update-Prüfung. Der Kroste-Standard verlangt sie im About-Dialog — bisher gab es
    /// sie nur in den Einstellungen, wo Nicht-Admins gar nicht hinkommen. null im Browser-Head,
    /// dort aktualisiert der Reload.
    /// </param>
    public InfoViewModel(UpdateCheckRunner? runUpdateCheck = null) => _runUpdateCheck = runUpdateCheck;

    public string AppName => "FlexFamily Calendar";
    public string AppVersion => UpdateService.CurrentVersion();
    public string Description => "Familienplaner für Arbeitszeiten, Schichten, Aktivitäten (Schule/Kita/Sport), Krankmeldungen und Schichttausch.";
    public string GitHubUrl => "https://github.com/Kroste/FlexFamily-Calendar";
    public string CoffeeUrl => "https://buymeacoffee.com/kroste";

    public event Action? CloseRequested;

    /// <summary>true, wenn dieser Head eine Update-Prüfung anbieten kann (Desktop).</summary>
    public bool CanCheckForUpdates => _runUpdateCheck is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _isCheckingForUpdates;

    [RelayCommand(CanExecute = nameof(CanRunUpdateCheck))]
    private async Task CheckForUpdatesAsync()
    {
        if (_runUpdateCheck is null) return;

        IsCheckingForUpdates = true;
        try
        {
            // force: true — die Intervall-Sperre des Auto-Checks gilt hier nicht, der Nutzer
            // hat ja gerade ausdrücklich gefragt.
            await _runUpdateCheck(force: true);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private bool CanRunUpdateCheck() => _runUpdateCheck is not null && !IsCheckingForUpdates;

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
