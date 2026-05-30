using CommunityToolkit.Mvvm.Input;
using FlexFamilyCalendar.Localization;
using FlexFamilyCalendar.Services.Update;

namespace FlexFamilyCalendar.ViewModels;

public enum UpdateDialogAction { Install, OpenReleasePage, Later, Skip }

public partial class UpdateViewModel : ViewModelBase
{
    public UpdateInfo Info { get; }

    public string HeaderText => Localizer.Instance["Update_Title"];
    public string SubHeader => string.Format(Localizer.Instance["Update_Description"], Info.CurrentVersion, Info.LatestVersion);
    public string ChangelogMarkdown => Info.ChangelogMarkdown;
    public bool HasAsset => Info.Asset is not null;
    public bool NoAsset => !HasAsset;

    public event Action<UpdateDialogAction?>? Closed;

    public UpdateViewModel(UpdateInfo info) => Info = info;

    [RelayCommand] private void Install() => Closed?.Invoke(UpdateDialogAction.Install);
    [RelayCommand] private void OpenReleasePage() => Closed?.Invoke(UpdateDialogAction.OpenReleasePage);
    [RelayCommand] private void Later() => Closed?.Invoke(UpdateDialogAction.Later);
    [RelayCommand] private void Skip() => Closed?.Invoke(UpdateDialogAction.Skip);
    [RelayCommand] private void Cancel() => Closed?.Invoke(null);
}
