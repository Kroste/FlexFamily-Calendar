using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Von/Bis-Datumsfelder dürfen sich nicht überlappen. Der Fluent-DatePicker hat eine Mindestbreite
/// von rund 300 Pixeln (Tag, Monat, Jahr als drei Felder); zwei davon nebeneinander passen in
/// keinen der Dialoge. Sie schoben sich übereinander — das Jahr des einen lag unter dem Tag des
/// anderen. Gemessen wird am echten gerenderten Layout, in der Breite, die das Dialogfenster hat.
/// </summary>
[Collection("Localizer")]
public class DateRangeLayoutTests : IClassFixture<HeadlessAppFixture>
{
    private readonly HeadlessAppFixture _app;
    public DateRangeLayoutTests(HeadlessAppFixture app) => _app = app;

    private static List<Rect> DatePickerBoundsIn(Window window)
        => window.GetVisualDescendants().OfType<DatePicker>()
                 .Where(d => d.IsEffectivelyVisible)
                 .Select(d => new Rect(d.TranslatePoint(new Point(0, 0), window)!.Value, d.Bounds.Size))
                 .ToList();

    private static void AssertNoOverlapAndInside(Window window, List<Rect> pickers)
    {
        Assert.Equal(2, pickers.Count);
        Assert.False(pickers[0].Intersects(pickers[1]),
            $"Datumsfelder überlappen: {pickers[0]} und {pickers[1]}");
        foreach (var r in pickers)
            Assert.True(r.Right <= window.Bounds.Width + 0.5,
                $"Datumsfeld ragt aus dem Fenster: {r}, Fensterbreite {window.Bounds.Width}");
    }

    [Fact]
    public Task Pause_dialog_date_fields_do_not_overlap()
        => _app.Session.Dispatch(() =>
        {
            var rule = new RecurringActivity
            {
                Id = "r1", Title = "Hinfahrt Kita",
                StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(9, 30, 0),
                Weekdays = { DayOfWeek.Monday, DayOfWeek.Tuesday }
            };
            // Breite wie RecurrencePauseDialog (Width="540").
            var window = new Window
            {
                Width = 540, Height = 700,
                Content = new RecurrencePauseView { DataContext = new RecurrencePauseViewModel(rule, new DateOnly(2026, 9, 18)) }
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            AssertNoOverlapAndInside(window, DatePickerBoundsIn(window));
            window.Close();
            return true;
        }, CancellationToken.None);

    [Fact]
    public Task Entry_dialog_absence_range_does_not_overlap()
        => _app.Session.Dispatch(() =>
        {
            var vm = new EntryEditorViewModel(new DateOnly(2026, 9, 18),
                new[] { new User { Id = "u1", Username = "mara", DisplayName = "Mara" } });
            vm.SelectedType = vm.EntryTypes.First(t => t.Type == EntryType.Vacation);

            // Schmalste Breite, die der Eintrag-Dialog zulässt (MinWidth 432 + Ränder).
            var window = new Window { Width = 480, Height = 900, Content = new EntryEditorView { DataContext = vm } };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            AssertNoOverlapAndInside(window, DatePickerBoundsIn(window));
            window.Close();
            return true;
        }, CancellationToken.None);
}
