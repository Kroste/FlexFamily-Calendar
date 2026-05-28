using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlexFamilyCalendar.Controls;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.ViewModels;

namespace FlexFamilyCalendar.Views;

public partial class CalendarView : UserControl
{
    private CalendarViewModel? _vm;
    private bool _scrolledToMorning;

    public CalendarView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_scrolledToMorning) return;
        _scrolledToMorning = true;
        // Nach dem ersten Layout auf ~07:00 scrollen, damit der Morgen sichtbar ist.
        Dispatcher.UIThread.Post(() =>
            GridScroll.Offset = GridScroll.Offset.WithY(7 * CalendarMetrics.PixelsPerHour),
            DispatcherPriority.Background);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm != null)
        {
            _vm.EntryDialogRequested -= OnEntryDialogRequested;
            _vm.SwapDialogRequested -= OnSwapDialogRequested;
            _vm.ReplanDialogRequested -= OnReplanDialogRequested;
        }

        _vm = DataContext as CalendarViewModel;

        if (_vm != null)
        {
            _vm.EntryDialogRequested += OnEntryDialogRequested;
            _vm.SwapDialogRequested += OnSwapDialogRequested;
            _vm.ReplanDialogRequested += OnReplanDialogRequested;
        }
    }

    /// <summary>Klick auf eine Eintragskarte → Bearbeiten. Tag wird per Referenzgleichheit gefunden.</summary>
    private void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if (_vm == null) return;
        if (sender is not Control { DataContext: CalendarEntry entry }) return;

        var day = _vm.Days.FirstOrDefault(d => d.Entries.Contains(entry));
        if (day != null)
            _vm.ActivateEntry(day.Date, entry);
    }

    private async void OnEntryDialogRequested(DateOnly date, CalendarEntry? existing, IReadOnlyList<User> users,
        bool canPickUser, IReadOnlyList<EntryType> allowedTypes, IReadOnlyList<ActivityType> activityTypes)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var vm = existing is null
                ? new EntryEditorViewModel(date, users, canPickUser, allowedTypes, activityTypes)
                : new EntryEditorViewModel(date, users, existing, canPickUser, allowedTypes, activityTypes);

            var dialog = new EntryEditorDialog { DataContext = vm };
            var result = await dialog.ShowDialog<EntryDialogResult?>(owner);

            if (result is not null && _vm is not null)
                await _vm.ApplyEntryResultAsync(date, result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Eintrag-Dialog", ex);
        }
    }

    private async void OnSwapDialogRequested(ShiftSwapViewModel vm)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var dialog = new ShiftSwapDialog { DataContext = vm };
            var result = await dialog.ShowDialog<SwapDialogResult?>(owner);

            if (result is not null && _vm is not null)
                await _vm.ApplySwapResultAsync(result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Tausch-Dialog", ex);
        }
    }

    private async void OnReplanDialogRequested(ReplanViewModel vm)
    {
        try
        {
            if (TopLevel.GetTopLevel(this) is not Window owner) return;

            var dialog = new ReplanDialog { DataContext = vm };
            var result = await dialog.ShowDialog<ReplanResult?>(owner);

            if (result is not null && _vm is not null)
                await _vm.ApplyReplanResultAsync(result);
        }
        catch (Exception ex)
        {
            LogService.Error("Fehler im Umplanungs-Dialog", ex);
        }
    }
}
