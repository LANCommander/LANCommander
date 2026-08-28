using System.Reflection;

namespace LANCommander.UI;

/// <summary>
/// Centralizes the URL used to load the LANCommander.UI JavaScript bundle.
/// </summary>
/// <remarks>
/// The bundle is loaded both by <c>&lt;script type="module"&gt;</c> tags and by dynamic
/// <c>import()</c> calls from JS interop. The browser keys its module registry (and its HTTP cache)
/// on the exact URL, so those URLs must match character for character. If they differ, the runtime
/// ends up with two separate module records and interop can resolve against a stale bundle, which
/// surfaces as "The value '&lt;function&gt;' is not a function".
/// </remarks>
public static class UIAssets
{
    private static readonly string Version =
        typeof(UIAssets).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UIAssets).Assembly.GetName().Version?.ToString()
        ?? "0";

    /// <summary>
    /// Root-relative, cache-busted URL of the UI bundle. Use this everywhere the bundle is loaded.
    /// </summary>
    public static readonly string BundleUrl = $"/_content/LANCommander.UI/bundle.js?v={Uri.EscapeDataString(Version)}";
}
