using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Tests;

// Onboarding-Dialog hat 4 Slides. Nach dem letzten Slide → Finish setzt CompletedFully=true.
// Skip vom ersten Slide aus → Dialog schließt, aber CompletedFully bleibt false (User will
// beim nächsten Login erneut gefragt werden).
public class OnboardingViewModelTests
{
    [Fact]
    public void Start_auf_erstem_Slide_Back_deaktiviert()
    {
        var vm = new OnboardingViewModel();
        Assert.Equal(0, vm.Index);
        Assert.False(vm.CanGoBack);
        Assert.True(vm.CanGoForward);
        Assert.False(vm.IsLastSlide);
    }

    [Fact]
    public void Next_bis_letzter_Slide_IsLastSlide_true_Forward_false()
    {
        var vm = new OnboardingViewModel();
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        Assert.Equal(3, vm.Index);
        Assert.True(vm.IsLastSlide);
        Assert.False(vm.CanGoForward);
        Assert.True(vm.CanGoBack);
    }

    [Fact]
    public void Finish_setzt_CompletedFully_true_und_feuert_CloseRequested()
    {
        var vm = new OnboardingViewModel();
        var fired = false;
        vm.CloseRequested += () => fired = true;

        vm.FinishCommand.Execute(null);

        Assert.True(vm.CompletedFully);
        Assert.True(fired);
    }

    [Fact]
    public void Skip_schließt_ohne_CompletedFully()
    {
        var vm = new OnboardingViewModel();
        var fired = false;
        vm.CloseRequested += () => fired = true;

        vm.SkipCommand.Execute(null);

        Assert.False(vm.CompletedFully);
        Assert.True(fired);
    }
}
