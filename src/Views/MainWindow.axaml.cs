using Avalonia.Controls;

namespace FlexFamilyCalendar.Views;

/// <summary>Dünne Window-Hülle um <see cref="MainView"/>. Logik (Dialog-Handler etc.) liegt in MainView,
/// damit dieselbe Oberfläche auch im Browser-Head (SingleView) funktioniert.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
