using Avalonia;

namespace LANCommander.Launcher.Avalonia.Helpers;

public static class Localization
{
    public static string Localize(string key, params object[] args)
    {
        string? s = null;

        // Pass null for themeVariant so non-theme-specific resources in merged
        // dictionaries are always found, regardless of which thread calls this.
        var app = Application.Current;
        if (app != null)
        {
            app.TryGetResource(key, null, out var resource);
            s = resource as string;
        }

        s ??= key;
        return args.Length == 0 ? s : string.Format(s, args);
    }
}
