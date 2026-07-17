using Avalonia.Controls;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class InfoDialog : ChromeWindow
{
    public InfoDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is InfoViewModel vm)
                vm.CloseRequested += Close;
        };
    }
}
