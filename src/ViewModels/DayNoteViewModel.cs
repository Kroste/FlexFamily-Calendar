using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>Ergebnis des Hinweis-Dialogs: Text + (optional) zugeordnete Person. null = abbrechen.</summary>
public record DayNoteResult(string Note, string? NoteUserId);

/// <summary>Kleiner Dialog für den Tages-Hinweis. Wenn keine Person ausgewählt ist, sehen alle den
/// Hinweis; sonst nur Admin und die zugeordnete Person.</summary>
public partial class DayNoteViewModel : ViewModelBase
{
    public DateOnly Date { get; }
    public string DateLabel { get; }
    public ObservableCollection<NoteAudienceItem> Audiences { get; } = new();

    [ObservableProperty] private string _note;
    [ObservableProperty] private NoteAudienceItem? _selectedAudience;

    public event Action<DayNoteResult?>? Closed;

    public DayNoteViewModel(DateOnly date, string note, string? noteUserId, IReadOnlyList<User> users)
    {
        Date = date;
        DateLabel = date.ToString("D", CultureInfo.CurrentCulture);
        _note = note;

        var allItem = new NoteAudienceItem(null, Localizer.Instance["DayNote_AudienceAll"]);
        Audiences.Add(allItem);
        foreach (var u in users.OrderBy(x => string.IsNullOrEmpty(x.DisplayName) ? x.Username : x.DisplayName,
                                       StringComparer.CurrentCultureIgnoreCase))
        {
            var name = string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName;
            Audiences.Add(new NoteAudienceItem(u.Id, name));
        }
        _selectedAudience = Audiences.FirstOrDefault(a => a.UserId == noteUserId) ?? allItem;
    }

    [RelayCommand]
    private void Save() => Closed?.Invoke(new DayNoteResult(Note ?? "", SelectedAudience?.UserId));

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}

public record NoteAudienceItem(string? UserId, string Name);
