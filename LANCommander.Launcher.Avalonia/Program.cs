using Avalonia;
using System;
using LANCommander.Launcher.Avalonia.Services;

namespace LANCommander.Launcher.Avalonia;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // If a protocol arg like "lancommander://game/{guid}" is present, this
        // is a secondary instance spawned by a notification click.  Forward the
        // navigation request to the already-running instance, then exit.
        foreach (var arg in args)
        {
            var gameId = SingleInstanceService.ParseProtocolArg(arg);
            if (gameId.HasValue)
            {
                SingleInstanceService.TrySendToServer($"navigate-game:{gameId}");
                return;
            }
        }

        BuildAvaloniaApp()
#if DEBUG
            .WithDeveloperTools()
#endif
            .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
