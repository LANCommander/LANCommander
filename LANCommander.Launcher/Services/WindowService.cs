using BootstrapBlazor.Components;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Startup;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using LANCommander.UI.Extensions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;

namespace LANCommander.Launcher.Services;

internal static class WindowService
{
    private static readonly List<WindowRef> _windowRefs = new();

    internal static void CreateWindow<TApp>(
        WindowOptions options,
        Action<PhotinoBlazorAppBuilder>? builderHook,
        Action<PhotinoBlazorApp>? appHook,
        string[] args) where TApp : ComponentBase
    {
        if (options.RootComponentType is null)
            options.RootComponentType = typeof(TApp);

        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        
        builderHook?.Invoke(builder);

        builder.AddSettings();

        builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddStandardLogging());
        builder.Services.AddOpenTelemetryDefaults("Launcher", false);

        builder.Services.AddCustomWindow();
        builder.Services.AddAntDesign();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddLANCommanderUI();
        builder.Services.AddLANCommanderClient<Settings.Settings>();
        builder.Services.AddLANCommanderLauncher(options =>
        {

        });
        
        builder.RootComponents.Add(options.RootComponentType, "app");

        var app = builder.Build();

        app
            .RegisterMainWindow()
            .RegisterMediaHandler()
            .RegisterNotificationHandler();
        
        // if (_windowRefs.Any())
        //     app.MainWindow.SetParentCheck(_windowRefs.First(wr => wr.Type == WindowType.Main).Window);
        
        if (appHook is not null)
            appHook(app);
        
        var connectionClient = app.Services.GetRequiredService<IConnectionClient>();

        connectionClient.ConnectAsync().Wait();

        if (options.Type == WindowType.Main)
            app.MainWindow.RegisterWindowClosingHandler((_, _) =>
            {
                CloseAllWindows();
                return false;
            });

        app.Run();
    }

    internal static void Focus(WindowType type)
    {
        var windowRef = _windowRefs.FirstOrDefault(wr => wr.Type == type);
        
        // if (windowRef is null)
        //     windowRef.Window.Focus();
    }

    internal static void CloseAllWindows()
    {
        foreach (var windowRef in _windowRefs)
            windowRef.Window.Close();
    }
}