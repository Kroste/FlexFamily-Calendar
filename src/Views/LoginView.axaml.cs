using Avalonia.Controls;
using Avalonia.Interactivity;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class LoginView : UserControl
{
    private readonly StorageService? _settingsStore;

    public LoginView()
    {
        InitializeComponent();
        // Browser-Head hat keinen Disk-Storage — Connection-Button bleibt im Web leer.
        if (!OperatingSystem.IsBrowser())
            _settingsStore = new StorageService();

        DataContextChanged += OnDataContextChanged;

        // Das Verbindungs-Label ist ein zusammengesetzter Text und deshalb kein
        // {loc:Tr}-Binding — der Sprachwechsel erreicht es nicht von selbst. Ohne dieses
        // Abo blieb die Zeile beim Umschalten auf Englisch sichtbar deutsch stehen.
        Localizer.Instance.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => Localizer.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => _ = RefreshConnectionLabelAsync();

    private async void OnDataContextChanged(object? sender, EventArgs e)
        => await RefreshConnectionLabelAsync();

    private async Task RefreshConnectionLabelAsync()
    {
        if (DataContext is not LoginViewModel vm) return;
        if (OperatingSystem.IsBrowser() || _settingsStore is null)
        {
            vm.ConnectionLabel = "";
            return;
        }
        try
        {
            var s = await _settingsStore.LoadSettingsAsync();
            vm.ConnectionLabel = s.UseServer && !string.IsNullOrWhiteSpace(s.ServerUrl)
                ? string.Format(Localizer.Instance["Connection_LabelServer"], s.ServerUrl)
                : Localizer.Instance["Connection_LabelLocal"];
        }
        catch (Exception ex)
        {
            LogService.Warn("Verbindungs-Label konnte nicht gelesen werden: {0}", ex.Message);
            vm.ConnectionLabel = Localizer.Instance["Connection_LabelLocal"];
        }
    }

    private async void OnConnectionClick(object? sender, RoutedEventArgs e)
    {
        if (OperatingSystem.IsBrowser() || _settingsStore is null) return;
        if (App.DialogService is null) { LogService.Warn("Connection-Dialog: kein Backend"); return; }

        try
        {
            var s = await _settingsStore.LoadSettingsAsync();
            var vm = new ConnectionSettingsViewModel(s.UseServer, s.ServerUrl);
            var result = await App.DialogService.ShowConnectionSettingsAsync(vm);
            if (result is null) return;

            s.UseServer = result.UseServer;
            s.ServerUrl = result.ServerUrl;
            await _settingsStore.SaveSettingsAsync(s);
            LogService.UserAction("?", result.UseServer
                ? $"Verbindung: Server {result.ServerUrl}"
                : "Verbindung: lokal (JSON)");

            // Label sofort aktualisieren — wirksam wird die Änderung erst beim Neustart.
            if (DataContext is LoginViewModel lvm)
            {
                lvm.ConnectionLabel = result.UseServer
                    ? string.Format(Localizer.Instance["Connection_LabelServer"], result.ServerUrl)
                    : Localizer.Instance["Connection_LabelLocal"];
                lvm.ErrorMessage = Localizer.Instance["Connection_RestartHint"];
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Connection-Dialog", ex);
        }
    }
}
