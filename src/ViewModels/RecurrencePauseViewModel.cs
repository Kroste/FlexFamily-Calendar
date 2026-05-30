using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Bearbeitet die Liste der Aussetzungen einer wiederkehrenden Aktivität.
/// Closed(IReadOnlyList) = Liste anwenden, Closed(null) = abbrechen.
/// </summary>
public partial class RecurrencePauseViewModel : ViewModelBase
{
    public string RuleTitle { get; }
    public string SubHeader { get; }

    public ObservableCollection<SkipRow> ExistingSkips { get; } = new();

    [ObservableProperty] private DateTimeOffset? _newFrom;
    [ObservableProperty] private DateTimeOffset? _newTo;
    [ObservableProperty] private string _newReason = "";
    [ObservableProperty] private string _errorMessage = "";

    public event Action<IReadOnlyList<RecurrenceSkip>?>? Closed;

    public RecurrencePauseViewModel(RecurringActivity rule, DateOnly clickedDate)
    {
        RuleTitle = rule.Title;
        SubHeader = string.Format(
            CultureInfo.CurrentCulture,
            Localizer.Instance["RecurrencePause_SubHeader"],
            rule.WeekdaysLabel,
            rule.TimeRange);

        foreach (var s in rule.Skips.OrderBy(x => x.From))
            ExistingSkips.Add(SkipRow.Create(s, this));

        // Vorbelegung: das angeklickte Datum als Startwert für eine neue Pause
        var dto = new DateTimeOffset(clickedDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        _newFrom = dto;
        _newTo = dto;
    }

    [RelayCommand]
    private void AddNewSkip()
    {
        ErrorMessage = "";
        if (NewFrom is null || NewTo is null)
        {
            ErrorMessage = Localizer.Instance["RecurrencePause_ErrorNoDate"];
            return;
        }
        var from = DateOnly.FromDateTime(NewFrom.Value.Date);
        var to = DateOnly.FromDateTime(NewTo.Value.Date);
        if (to < from) (from, to) = (to, from);

        var skip = new RecurrenceSkip { From = from, To = to, Reason = string.IsNullOrWhiteSpace(NewReason) ? null : NewReason.Trim() };
        ExistingSkips.Add(SkipRow.Create(skip, this));

        // Felder für eine evtl. weitere Pause auf den nächsten Tag setzen
        NewReason = "";
    }

    internal void RemoveSkip(SkipRow row) => ExistingSkips.Remove(row);

    [RelayCommand]
    private void Save()
    {
        // Achtung: KEIN impliziter Add aus NewFrom/NewTo. Die vorherige Logik kollidierte
        // mit dem Lösch-Workflow — wer einen bestehenden Skip per X entfernt und Speichert,
        // bekam wegen der Datumsvorbelegung sofort einen Ersatz für den angeklickten Tag
        // („löschen geht nicht / wird dem Klick-Tag zugeordnet").
        // Persistiert wird ausschließlich die Liste, wie sie der User über „Pause hinzufügen"
        // bzw. die X-Buttons gepflegt hat.
        var result = ExistingSkips
            .OrderBy(r => r.From)
            .Select(r => new RecurrenceSkip { Id = r.Id, From = r.From, To = r.To, Reason = r.Reason })
            .ToList();
        Services.LogService.Debug("Pause-Save: skips={0} → {1}",
            result.Count,
            string.Join(", ", result.Select(r => $"{r.From}..{r.To}")));
        Closed?.Invoke(result);
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}

public partial class SkipRow : ObservableObject
{
    private readonly RecurrencePauseViewModel _parent;

    public string Id { get; }
    public DateOnly From { get; }
    public DateOnly To { get; }
    public string? Reason { get; }

    public string RangeLabel { get; }
    public string ReasonLabel { get; }

    public IRelayCommand RemoveCommand { get; }

    private SkipRow(RecurrencePauseViewModel parent, string id, DateOnly from, DateOnly to, string? reason)
    {
        _parent = parent;
        Id = id;
        From = from;
        To = to;
        Reason = reason;
        RangeLabel = from == to
            ? from.ToString("d", CultureInfo.CurrentCulture)
            : $"{from.ToString("d", CultureInfo.CurrentCulture)} – {to.ToString("d", CultureInfo.CurrentCulture)}";
        ReasonLabel = string.IsNullOrWhiteSpace(reason) ? "" : reason;
        RemoveCommand = new RelayCommand(() => _parent.RemoveSkip(this));
    }

    public static SkipRow Create(RecurrenceSkip s, RecurrencePauseViewModel parent) =>
        new(parent, s.Id, s.From, s.To, s.Reason);
}
