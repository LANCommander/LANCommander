using Avalonia;
using CommandLine;
using LANCommander.Packager.Models;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Packager;

internal static class Program
{
    public static PackageContext Context { get; } = new();
    public static IServiceProvider Services { get; private set; } = null!;

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

        ConfigureServices();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        services.AddHttpClient();

        var configurationBuilder = new ConfigurationBuilder();
        var configuration = configurationBuilder.ReadFromFile<PackagerSettings>();
        var refresher = configurationBuilder.ReadFromServer<PackagerSettings>(configuration);
        configuration = configurationBuilder.Build();

        services.Configure<PackagerSettings>(configuration);
        services.AddSingleton(refresher);

        services.AddLANCommanderClient<PackagerSettings>();

        Services = services.BuildServiceProvider();
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
