using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlexFamilyCalendar.Models;
using FlexFamilyCalendar.Services;
using FlexFamilyCalendar.Services.AI;
using FlexFamilyCalendar.ViewModels;
using FlexFamilyCalendar.Views;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Klick-Tests auf der echten Plantabelle. Auf ViewModel-Ebene sind sie nicht abbildbar: die
/// Fehler, um die es hier geht, sitzen ausschließlich im Event-Routing zwischen Zeile, Zelle
/// und den Knöpfen darin.
/// </summary>
[Collection("Localizer")]
public class CalendarCellInputTests : IClassFixture<HeadlessAppFixture>
{
    private readonly HeadlessAppFixture _app;

    public CalendarCellInputTests(HeadlessAppFixture app) => _app = app;

    private sealed record DialogRequest(DateOnly Date, bool IsNew);

    private sealed record Harness(Window Window, CalendarViewModel Vm, List<DialogRequest> Requests);

    /// <summary>Eine Woche mit zwei Personen; die Admin-Zeile hat am Montag genau einen
    /// Arbeitseintrag. Damit ist dort <c>ShowAddMoreButton</c> aktiv — der Fall aus dem Bugreport.</summary>
    private static async Task<Harness> BuildAsync()
    {
        var monday = MondayOfThisWeek();
        var admin = new User
        {
            Id = "admin", Username = "lars", DisplayName = "Lars",
            Role = UserRole.Admin, Category = PersonCategory.Parent, Color = "#E67E22"
        };
        var employee = new User
        {
            Id = "emp", Username = "mara", DisplayName = "Mara",
            Role = UserRole.User, Category = PersonCategory.Employee, Color = "#2E86C1"
        };

        var storage = new InMemoryStorageService();
        await storage.SaveUsersAsync(new List<User> { admin, employee });
        await storage.SaveDayAsync(new CalendarDay
        {
            DateString = monday.ToString("yyyy-MM-dd"),
            Entries = new List<CalendarEntry>
            {
                new()
                {
                    Id = "e1", UserId = admin.Id, UserDisplayName = admin.DisplayName,
                    Type = EntryType.Work,
                    StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(15, 0, 0)
                }
            }
        });

        var vm = new CalendarViewModel(storage, admin, new NotificationService(storage),
            new AiService(Array.Empty<IAiProvider>()), new LocalMailSender(storage));
        await vm.RefreshAllAsync(silent: true);

        var requests = new List<DialogRequest>();
        vm.EntryDialogRequested += (date, existing, _, _, _, _) => requests.Add(new DialogRequest(date, existing is null));

        var window = new Window { Width = 1400, Height = 900, Content = new CalendarView { DataContext = vm } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Harness(window, vm, requests);
    }

    private static DateOnly MondayOfThisWeek()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var dow = (int)today.DayOfWeek;
        return today.AddDays(-(dow == 0 ? 6 : dow - 1));
    }

    /// <summary>Linksklick auf die Mitte des Controls, so wie ihn ein Nutzer auslöst.</summary>
    private static void Click(Window window, Visual target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window);
        Assert.NotNull(center);
        window.MouseDown(center!.Value, MouseButton.Left);
        window.MouseUp(center.Value, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Button AddMoreButton(Window window)
        => window.GetVisualDescendants()
                 .OfType<Button>()
                 .Single(b => b.DataContext is PersonDayCellViewModel && b.IsEffectivelyVisible);

    private static Button PersonButton(Window window, string userId)
        => window.GetVisualDescendants()
                 .OfType<Button>()
                 .First(b => b.DataContext is PersonRowViewModel r && r.UserId == userId);

    /// <summary>
    /// Der Personen-Knopf ist ein gewöhnlicher Button mitten in der Zeile — und damit der
    /// Prüfstein dafür, dass die Pointer-Handler der Zeile keinem Button den Click wegnehmen.
    /// Nimmt <c>OnRowPointerReleased</c> beim Loslassen wieder das Pointer-Capture zurück
    /// (was es ohne echten Drag nicht darf), setzt Avalonias Button intern IsPressed=false und
    /// verwirft seinen Click: der Knopf sieht aus wie ein Knopf und tut nichts.
    /// </summary>
    [Fact]
    public Task PersonButton_StartsImpersonation()
        => _app.Session.Dispatch(async () =>
        {
            var h = await BuildAsync();
            Assert.Null(h.Vm.ViewAsUserId);

            Click(h.Window, PersonButton(h.Window, "emp"));

            Assert.Equal("emp", h.Vm.ViewAsUserId);
            return true;
        }, CancellationToken.None);

    [Fact]
    public Task AddMoreButton_RequestsNewEntry()
        => _app.Session.Dispatch(async () =>
        {
            var h = await BuildAsync();

            Click(h.Window, AddMoreButton(h.Window));

            // Genau einmal und als NEUER Eintrag: fiele der Klick auf den Chip darunter durch,
            // käme stattdessen der Editor des bestehenden Eintrags.
            var request = Assert.Single(h.Requests);
            Assert.True(request.IsNew);
            return true;
        }, CancellationToken.None);

    [Fact]
    public Task EmptyCell_RequestsNewEntry()
        => _app.Session.Dispatch(async () =>
        {
            var h = await BuildAsync();

            var empty = h.Window.GetVisualDescendants()
                         .OfType<Border>()
                         .First(b => b.DataContext is PersonDayCellViewModel { IsEmpty: true, CanAdd: true });
            Click(h.Window, empty);

            var request = Assert.Single(h.Requests);
            Assert.True(request.IsNew);
            return true;
        }, CancellationToken.None);

    [Fact]
    public Task ExistingEntry_OpensEditorInsteadOfNewEntry()
        => _app.Session.Dispatch(async () =>
        {
            var h = await BuildAsync();

            var chip = h.Window.GetVisualDescendants()
                        .OfType<Border>()
                        .First(b => b.DataContext is CalendarEntry);
            Click(h.Window, chip);

            // OnEntryTapped stoppt das Bubbling — sonst legte derselbe Klick zusätzlich einen
            // neuen Eintrag an.
            var request = Assert.Single(h.Requests);
            Assert.False(request.IsNew);
            return true;
        }, CancellationToken.None);
}
