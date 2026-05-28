using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Kleiner Dialog für den allgemeinen Tages-Hinweis (Admin). Closed(string) = speichern, Closed(null) = abbrechen.</summary>
public partial class DayNoteViewModel : ViewModelBase
{
    public DateOnly Date { get; }
    public string DateLabel { get; }

    [ObservableProperty] private string _note;

    public event Action<string?>? Closed;

    public DayNoteViewModel(DateOnly date, string note)
    {
        Date = date;
        DateLabel = date.ToString("D", CultureInfo.CurrentCulture);
        _note = note;
    }

    [RelayCommand]
    private void Save() => Closed?.Invoke(Note ?? "");

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
