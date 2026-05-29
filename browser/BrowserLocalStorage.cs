using System.Runtime.InteropServices.JavaScript;
using FlexFamilyCalendar.Services;

namespace FlexFamilyCalendar.Browser;

/// <summary>Implementiert <see cref="IBrowserKeyValueStore"/> über die JS-localStorage.</summary>
public partial class BrowserLocalStorage : IBrowserKeyValueStore
{
    public string? Get(string key) => JsLocalStorage.GetItem(key);

    public void Set(string key, string value) => JsLocalStorage.SetItem(key, value);

    private static partial class JsLocalStorage
    {
        [JSImport("globalThis.localStorage.getItem")]
        public static partial string? GetItem(string key);

        [JSImport("globalThis.localStorage.setItem")]
        public static partial void SetItem(string key, string value);
    }
}
