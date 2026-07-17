using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class OnboardingDialog : ChromeWindow
{
    public OnboardingDialog()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is OnboardingViewModel vm)
                vm.CloseRequested += Close;
        };
    }
}
