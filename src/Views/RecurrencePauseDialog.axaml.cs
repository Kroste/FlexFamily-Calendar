using Avalonia.Controls;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class RecurrencePauseDialog : ChromeWindow
{
    private RecurrencePauseViewModel? _vm;

    public RecurrencePauseDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as RecurrencePauseViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(IReadOnlyList<RecurrenceSkip>? result) => Close(result);
}
