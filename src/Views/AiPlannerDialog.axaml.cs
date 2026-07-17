using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class AiPlannerDialog : ChromeWindow
{
    private AiPlannerViewModel? _vm;

    public AiPlannerDialog() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_vm != null) _vm.CloseRequested -= OnCloseRequested;
        _vm = DataContext as AiPlannerViewModel;
        if (_vm != null) _vm.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested() => Close();
}
