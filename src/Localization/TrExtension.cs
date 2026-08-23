using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace FlexFamilyCalendar.Localization;

/// <summary>
/// XAML-Kurzform <c>{loc:Tr Key}</c>: bindet auf den gecachten
/// <see cref="LocalizedString"/> zum Schlüssel. Beim Sprachwechsel feuert der Wrapper ein
/// reguläres PropertyChanged, und alle Bindings in allen Fenstern aktualisieren live.
/// </summary>
public sealed class TrExtension : MarkupExtension
{
    public TrExtension() { }
    public TrExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
        // Wrapper aus dem statischen Cache holen, NICHT pro Binding neu erzeugen: Avalonia
        // hält Binding.Source nicht stark, ein frischer Wrapper wäre nach dem ersten
        // Rendering weg und der Sprachwechsel liefe für dieses Element ins Leere.
        => new Binding(nameof(LocalizedString.Value))
        {
            Source = LocalizedString.Get(Key),
            Mode = BindingMode.OneWay
        };
}
