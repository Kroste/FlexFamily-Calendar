using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace FlexFamilyCalendar.Services.Update;

/// <summary>
/// Fragt die GitHub-Releases-API nach dem aktuellsten Release und prüft, ob die laufende
/// App-Version älter ist. Wählt das Asset, das zur aktuellen Plattform passt
/// (linux-x64.tar.gz / x86_64.AppImage / win-x64.zip).
/// </summary>
public class UpdateService
{
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repo;

    public UpdateService(string owner, string repo, HttpClient? http = null)
    {
        _owner = owner;
        _repo = repo;
        _http = http ?? new HttpClient();
        // GitHub verlangt einen User-Agent, sonst 403.
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("FlexFamilyCalendar", "1.0"));
    }

    /// <summary>Version der laufenden Assembly (vom Build via -p:Version gesetzt).</summary>
    public static string CurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // CLR hängt manchmal "+sha" an die Informational-Version → vor dem Vergleich abschneiden.
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    /// <summary>Welches Asset-Schema zur laufenden Plattform passt — auch entscheidet, ob Update überhaupt geht.</summary>
    public static UpdatePlatform DetectPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return UpdatePlatform.WindowsZip;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // appimagetool exportiert $APPIMAGE = Pfad zum laufenden AppImage. Wenn gesetzt
            // → Self-Update muss die AppImage-Datei ersetzen, nicht das entpackte Binary.
            return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPIMAGE"))
                ? UpdatePlatform.LinuxTar
                : UpdatePlatform.LinuxAppImage;
        }
        return UpdatePlatform.Unsupported;
    }

    /// <summary>Fragt /releases/latest und liefert ein UpdateInfo, wenn ein neueres Release existiert.</summary>
    public Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
        => CheckAsync(CurrentVersion(), DetectPlatform(), ct);

    /// <summary>Tests-Overload: aktuelle Version + Plattform explizit übergeben.</summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion, UpdatePlatform platform, CancellationToken ct = default)
    {
        if (platform == UpdatePlatform.Unsupported) return null;

        var url = $"https://api.github.com/repos/{_owner}/{_repo}/releases/latest";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var body = await resp.Content.ReadAsStringAsync(ct);
        return Parse(body, currentVersion, platform);
    }

    /// <summary>Pure JSON→UpdateInfo (testbar ohne HttpClient).</summary>
    public static UpdateInfo? Parse(string releaseJson, string currentVersion, UpdatePlatform platform)
    {
        var release = JObject.Parse(releaseJson);
        var tag = release["tag_name"]?.ToString() ?? "";
        if (string.IsNullOrEmpty(tag)) return null;
        if (!VersionCompare.IsNewer(tag, currentVersion)) return null;

        var assets = release["assets"] as JArray ?? new JArray();
        var asset = PickAsset(assets, platform);

        return new UpdateInfo(
            CurrentVersion: currentVersion,
            LatestVersion: tag,
            ReleaseUrl: release["html_url"]?.ToString() ?? "",
            ChangelogMarkdown: release["body"]?.ToString() ?? "",
            Asset: asset);
    }

    private static UpdateAsset? PickAsset(JArray assets, UpdatePlatform platform)
    {
        var suffix = platform switch
        {
            UpdatePlatform.LinuxTar => "linux-x64.tar.gz",
            UpdatePlatform.LinuxAppImage => "x86_64.AppImage",
            UpdatePlatform.WindowsZip => "win-x64.zip",
            _ => null
        };
        if (suffix is null) return null;

        foreach (var a in assets)
        {
            var name = a["name"]?.ToString() ?? "";
            if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            return new UpdateAsset(
                FileName: name,
                DownloadUrl: a["browser_download_url"]?.ToString() ?? "",
                Size: a["size"]?.Value<long>() ?? 0,
                Platform: platform);
        }
        return null;
    }

    /// <summary>Lädt das Update-Asset binär (Tests können den HttpClient stubben).</summary>
    public async Task DownloadAsync(UpdateAsset asset, string targetPath, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        var total = resp.Content.Headers.ContentLength ?? asset.Size;
        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(targetPath);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }
    }
}
