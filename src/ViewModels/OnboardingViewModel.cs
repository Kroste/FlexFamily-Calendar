using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;

namespace FlexFamilyCalendar.ViewModels;

/// <summary>
/// Erst-Login-Willkommens-Dialog. Zeigt die Kern-Bedienelemente in vier Slides und schließt
/// mit „Verstanden" — setzt dann <c>User.OnboardingSeen = true</c>, damit der Dialog beim
/// nächsten Login nicht mehr erscheint. Wenn der Benutzer stattdessen „Später zeigen" wählt,
/// bleibt <c>OnboardingSeen</c> false und der Dialog kommt beim nächsten Login erneut.
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
    private readonly List<string[]> _slides = new()
    {
        // Titel-Key, Body-Key
        new[] { "Onboarding_Slide1_Title", "Onboarding_Slide1_Body" },
        new[] { "Onboarding_Slide2_Title", "Onboarding_Slide2_Body" },
        new[] { "Onboarding_Slide3_Title", "Onboarding_Slide3_Body" },
        new[] { "Onboarding_Slide4_Title", "Onboarding_Slide4_Body" }
    };

    [ObservableProperty] private int _index;

    public string Title => Localizer.Instance[_slides[Index][0]];
    public string Body => Localizer.Instance[_slides[Index][1]];
    public string StepLabel => $"{Index + 1} / {_slides.Count}";
    public bool CanGoBack => Index > 0;
    public bool CanGoForward => Index < _slides.Count - 1;
    public bool IsLastSlide => Index == _slides.Count - 1;

    /// <summary>true = Dialog wurde bis zum Ende gesehen und darf permanent ausgeblendet werden.</summary>
    public bool CompletedFully { get; private set; }

    public event Action? CloseRequested;

    partial void OnIndexChanged(int value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
        OnPropertyChanged(nameof(IsLastSlide));
    }

    [RelayCommand]
    private void Next()
    {
        if (CanGoForward) Index++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CanGoBack) Index--;
    }

    [RelayCommand]
    private void Finish()
    {
        CompletedFully = true;
        CloseRequested?.Invoke();
    }

    [RelayCommand]
    private void Skip() => CloseRequested?.Invoke();
}
