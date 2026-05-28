using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class DayNoteDialog : Window
{
    private DayNoteViewModel? _vm;

    public DayNoteDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.Closed -= OnClosed;
        _vm = DataContext as DayNoteViewModel;
        if (_vm != null) _vm.Closed += OnClosed;
    }

    private void OnClosed(string? note) => Close(note);
}
