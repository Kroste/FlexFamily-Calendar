using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.ViewModels;

public record MoveCopyResult(MoveCopyAction Action);

/// <summary>VM für den „Verschieben oder Kopieren?"-Dialog beim Drag&amp;Drop einer Schicht.</summary>
public partial class MoveCopyViewModel : ViewModelBase
{
    public string HeaderText { get; }
    public string Description { get; }

    public event Action<MoveCopyResult?>? Closed;

    public MoveCopyViewModel(string headerText, string description)
    {
        HeaderText = headerText;
        Description = description;
    }

    [RelayCommand]
    private void Move() => Closed?.Invoke(new MoveCopyResult(MoveCopyAction.Move));

    [RelayCommand]
    private void Copy() => Closed?.Invoke(new MoveCopyResult(MoveCopyAction.Copy));

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(null);
}
