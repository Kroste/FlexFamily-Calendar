using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

public record ConnectionSettingsResult(bool UseServer, string ServerUrl);

/// <summary>
/// Login-naher Mini-Dialog: lokal (JSON) vs. Server-Modus + Server-URL umstellen. Wird vor
/// dem ersten Login angeboten, damit der User nicht in der settings.json wühlen muss.
/// </summary>
public partial class ConnectionSettingsViewModel : ViewModelBase
{
    [ObservableProperty] private bool _useServer;
    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private string _errorMessage = "";

    public event Action<ConnectionSettingsResult?>? Closed;

    public ConnectionSettingsViewModel(bool useServer, string serverUrl)
    {
        _useServer = useServer;
        _serverUrl = serverUrl ?? "";
    }

    [RelayCommand]
    private void Save()
    {
        if (UseServer)
        {
            var url = (ServerUrl ?? "").Trim();
            if (string.IsNullOrEmpty(url))
            {
                ErrorMessage = Localizer.Instance["Connection_ErrorUrlMissing"];
                return;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ErrorMessage = Localizer.Instance["Connection_ErrorUrlInvalid"];
                return;
            }
            ServerUrl = url;
        }
        Closed?.Invoke(new ConnectionSettingsResult(UseServer, UseServer ? ServerUrl.Trim() : ""));
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
