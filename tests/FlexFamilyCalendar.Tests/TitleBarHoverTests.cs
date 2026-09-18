using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlexFamilyCalendar.Views;
using Xunit;

namespace FlexFamilyCalendar.Tests;

/// <summary>
/// Hover über den Titelleisten-Knöpfen, mit echter Mausbewegung gemessen. Fluent setzt beim
/// Hover Hintergrund UND Schrift am ContentPresenter im Button-Template — ein Style am Button
/// selbst verliert dagegen. Vorher wurde das Schließen-Kreuz deshalb schwarz auf Grau.
/// </summary>
[Collection("Localizer")]
public class TitleBarHoverTests : IClassFixture<HeadlessAppFixture>
{
    private readonly HeadlessAppFixture _app;
    public TitleBarHoverTests(HeadlessAppFixture app) => _app = app;

    private static (Color Background, Color Foreground) HoverColors(ChromeWindow window, string buttonName)
    {
        var button = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == buttonName);
        var center = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseMove(center);
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsPointerOver, $"{buttonName} bekam keinen Hover");
        var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        var bg = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
        var fg = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Foreground).Color;
        return (bg, fg);
    }

    private static ChromeWindow Open()
    {
        var window = new ChromeWindow { Width = 500, Height = 300, Content = new TextBlock { Text = "x" } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [Fact]
    public Task Close_hover_is_red_with_a_white_cross()
        => _app.Session.Dispatch(() =>
        {
            var window = Open();
            var (bg, fg) = HoverColors(window, "PART_CloseButton");

            Assert.Equal(Colors.White, fg);
            Assert.True(bg.R > 180 && bg.G < 90 && bg.B < 90, $"Kein Rot beim Schließen-Hover: {bg}");
            window.Close();
            return true;
        }, CancellationToken.None);

    [Fact]
    public Task Minimize_hover_keeps_the_white_symbol()
        => _app.Session.Dispatch(() =>
        {
            var window = Open();
            var (_, fg) = HoverColors(window, "PART_MinimizeButton");

            Assert.Equal(Colors.White, fg);
            window.Close();
            return true;
        }, CancellationToken.None);
}
