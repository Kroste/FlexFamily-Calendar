using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace FlexFamilyCalendar.Localization;

/// <summary>XAML-Kurzform: {loc:Tr Key} → Live-Binding auf Localizer[Key].</summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
        => new Binding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay
        };
}
