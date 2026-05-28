using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

public record GermanStateOption(GermanState State, string Name);

/// <summary>App-weite Einstellungen (Admin): Bundesland für Feiertage + pauschale Übernachtungs-Gutschrift.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private AppSettings _settings = new();

    public IReadOnlyList<GermanStateOption> States { get; }

    [ObservableProperty] private GermanStateOption? _selectedState;
    [ObservableProperty] private string _overnightHours = "2";
    [ObservableProperty] private string _statusMessage = "";

    public SettingsViewModel(StorageService storage)
    {
        _storage = storage;
        States = Enum.GetValues<GermanState>()
            .Select(s => new GermanStateOption(s, GermanStates.Names[s]))
            .OrderBy(o => o.Name)
            .ToList();
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        _settings = await _storage.LoadSettingsAsync();
        var current = GermanStates.Parse(_settings.HolidayState);
        SelectedState = States.FirstOrDefault(o => o.State == current) ?? States[0];
        OvernightHours = _settings.OvernightHoursPerDay.ToString(CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedState != null)
            _settings.HolidayState = SelectedState.State.ToString();
        if (double.TryParse(OvernightHours, NumberStyles.Any, CultureInfo.CurrentCulture, out var o) && o >= 0)
            _settings.OvernightHoursPerDay = o;

        await _storage.SaveSettingsAsync(_settings);
        StatusMessage = Localizer.Instance["Settings_Saved"];
        LogService.UserAction("Admin", "Einstellungen gespeichert");
    }
}
