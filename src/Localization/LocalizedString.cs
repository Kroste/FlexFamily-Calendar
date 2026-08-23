using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FlexFamilyCalendar.Localization;

/// <summary>
/// Bindbarer Wrapper um einen einzelnen Übersetzungs-Schlüssel. <see cref="TrExtension"/>
/// holt ihn über <see cref="Get"/> aus dem statischen Cache und bindet im XAML gegen
/// <see cref="Value"/>.
///
/// <para><b>Warum nicht direkt gegen den Indexer binden?</b> Ein Binding auf
/// <c>Localizer.Instance[Key]</c> braucht die WPF-Konvention
/// <c>PropertyChanged("Item[]")</c>. Avalonia 12 verarbeitet die nur unzuverlässig:
/// Bindings in Fenstern ohne Fokus bleiben stale. Genau so lief es hier vorher — ein
/// Sprachwechsel im Profil-Editor aktualisierte den Editor, aber nicht das Hauptfenster
/// dahinter.</para>
///
/// <para><b>Warum statisch gecacht?</b> Avalonia hält <c>Binding.Source</c> nicht dauerhaft
/// stark. Ein pro Binding frisch erzeugter Wrapper wird kurz nach dem ersten Rendering vom
/// GC eingesammelt, und die Sprachwechsel-Benachrichtigung läuft ins Leere — auch eine
/// WeakReference-Registry löst das nicht. Der Cache hält pro Schlüssel genau eine Instanz
/// für die App-Lebensdauer (hier gut 400 Stück, wenige KB).</para>
/// </summary>
public sealed class LocalizedString : INotifyPropertyChanged
{
    private static readonly Dictionary<string, LocalizedString> Cache = new(StringComparer.Ordinal);
    private static readonly Lock Sync = new();

    private LocalizedString(string key) => Key = key;

    public string Key { get; }

    public string Value => Localizer.Instance[Key];

    /// <summary>
    /// Liefert den gecachten Wrapper zum Schlüssel — beim ersten Zugriff erzeugt, danach
    /// wiederverwendet, damit alle Bindings auf denselben Schlüssel dieselbe Quelle teilen
    /// und garantiert am Leben bleiben.
    /// </summary>
    public static LocalizedString Get(string key)
    {
        lock (Sync)
        {
            if (!Cache.TryGetValue(key, out var s))
            {
                s = new LocalizedString(key);
                Cache[key] = s;
            }
            return s;
        }
    }

    /// <summary>Anzahl der gecachten Wrapper — nur für Diagnose und Tests.</summary>
    internal static int CachedCount
    {
        get { lock (Sync) return Cache.Count; }
    }

    /// <summary>
    /// Feuert <c>PropertyChanged(nameof(Value))</c> auf jedem gecachten Wrapper. Ruft der
    /// <see cref="Localizer"/> beim Sprachwechsel auf — dadurch aktualisieren alle Fenster
    /// gleichzeitig, nicht nur das aktive.
    /// </summary>
    internal static void NotifyAllChanged()
    {
        LocalizedString[] snapshot;
        lock (Sync) snapshot = [.. Cache.Values];
        foreach (var s in snapshot) s.OnPropertyChanged(nameof(Value));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
