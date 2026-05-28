using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

public record GermanStateOption(GermanState State, string Name);

/// <summary>Admin wählt das Bundesland für die Feiertagsberechnung (global in AppSettings).</summary>
public partial class HolidaySettingsViewModel : ViewModelBase
{
    private readonly StorageService _storage;
    private AppSettings _settings = new();

    public IReadOnlyList<GermanStateOption> States { get; }
    [ObservableProperty] private GermanStateOption? _selectedState;

    public event Action? Closed;

    public HolidaySettingsViewModel(StorageService storage)
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
    }

    [RelayCommand]
    private async Task Save()
    {
        if (SelectedState != null)
        {
            _settings.HolidayState = SelectedState.State.ToString();
            await _storage.SaveSettingsAsync(_settings);
        }
        Closed?.Invoke();
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke();
}
