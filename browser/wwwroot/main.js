import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window !== "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// Browser-Origin für den .NET-Head — HttpClient braucht eine ABSOLUTE BaseAddress,
// sonst löst er gegen die Page-Location auf (und unter file:// wird daraus file:///api/...).
globalThis.flexFamilyGetOrigin = () => globalThis.location.origin;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();
await dotnetRuntime.runMain(config.mainAssemblyName, [window.location.search]);

// Loading-Hinweis ausblenden, sobald Avalonia gerendert hat.
document.getElementById('loading')?.remove();
