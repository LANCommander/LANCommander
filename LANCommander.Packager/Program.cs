using Avalonia;
using CommandLine;
using LANCommander.Packager.Models;

namespace LANCommander.Packager;

internal static class Program
{
    public static PackageContext Context { get; } = new();

    [STAThread]
    public static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(options =>
            {
                if (!string.IsNullOrWhiteSpace(options.InstallerPath))
                    Context.InstallerPath = options.InstallerPath;
                
                if (!string.IsNullOrWhiteSpace(options.OutputPath))
                    Context.OutputPath = options.OutputPath;
            });

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
