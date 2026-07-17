using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels.Mobile;

/// <summary>
/// Mobile-Formular für Krank-/Urlaubsmeldung. Erzeugt über <see cref="AbsencePlanner"/> für jeden
/// Tag im Bereich einen Eintrag mit gemeinsamer AbsenceGroupId und speichert sie tageweise.
/// Nutzt bewusst nur die Datumsbereich- und Typ-Auswahl — keine Uhrzeit, keine Kategorie, kein
/// User-Picker (der angemeldete User meldet für sich selbst).
/// </summary>
public partial class MobileAbsenceViewModel : ObservableObject
{
    private readonly IStorageService _storage;
    private readonly User _user;

    public record AbsenceTypeOption(EntryType Type, string Label);

    public ObservableCollection<AbsenceTypeOption> Types { get; }

    [ObservableProperty] private AbsenceTypeOption? _selectedType;
    [ObservableProperty] private DateTimeOffset? _from = DateTimeOffset.Now;
    [ObservableProperty] private DateTimeOffset? _to = DateTimeOffset.Now;
    [ObservableProperty] private string _reason = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isSaving;

    public MobileAbsenceViewModel(IStorageService storage, User user, EntryType initialType)
    {
        _storage = storage;
        _user = user;

        var l = Localizer.Instance;
        Types = new ObservableCollection<AbsenceTypeOption>
        {
            new(EntryType.SickLeave,    l[EntryTypeInfo.Key(EntryType.SickLeave)]),
            new(EntryType.Vacation, l[EntryTypeInfo.Key(EntryType.Vacation)]),
            new(EntryType.Absence, l[EntryTypeInfo.Key(EntryType.Absence)])
        };
        _selectedType = Types.FirstOrDefault(t => t.Type == initialType) ?? Types[0];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedType is null || From is null || To is null)
        {
            StatusMessage = Localizer.Instance["Mobile_Absence_Missing"];
            return;
        }

        var from = DateOnly.FromDateTime(From.Value.Date);
        var to = DateOnly.FromDateTime(To.Value.Date);
        if (to < from) (from, to) = (to, from);

        IsSaving = true;
        StatusMessage = "";
        try
        {
            var template = new CalendarEntry
            {
                UserId = _user.Id,
                UserDisplayName = _user.DisplayName,
                Type = SelectedType.Type,
                StartTime = new TimeSpan(0, 0, 0),
                EndTime = new TimeSpan(23, 59, 0),
                Title = "",
                Notes = (Reason ?? "").Trim()
            };
            var groupId = Guid.NewGuid().ToString();
            var perDay = AbsencePlanner.Build(template, from, to, groupId);

            foreach (var (date, entry) in perDay)
            {
                var day = await _storage.LoadDayAsync(date);
                day.Entries.RemoveAll(e => e.UserId == _user.Id && EntryTypeInfo.IsAbsence(e.Type) && e.AbsenceGroupId == groupId);
                day.Entries.Add(entry);
                await _storage.SaveDayAsync(day);
            }

            StatusMessage = Localizer.Instance["Mobile_Absence_Saved"];
            Reason = "";
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler beim Speichern der Abwesenheit (Mobile)", ex);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }
}
