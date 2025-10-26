using System.Web;
using LANCommander.Launcher.Enums;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Startup;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Photino.Blazor.CustomWindow.Extensions;
using Photino.NET;
using Serilog;

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
        if (_windowRefs.Any(wf => wf.Type == options.Type))
            return;
        
        if (options.RootComponentType == null)
            options.RootComponentType = typeof(TApp);
        
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        if (builderHook != null)
            builderHook(builder);

        builder.AddLogging();
        
        builder.Services.AddCustomWindow();
        builder.Services.AddAntDesign();
        builder.Services.AddSingleton<LocalizationService>();
        builder.Services.AddLANCommanderClient<Settings>();
        builder.Services.AddLANCommanderLauncher(options =>
        {
            
        });
        
        builder.RootComponents.Add(options.RootComponentType, "app");
        
        var app = builder.Build();

        app
            .RegisterMediaHandler()
            .MainWindow
            .SetTitle(options.Title)
            .SetChromeless(true)
            .RegisterWebMessageReceivedHandler(ChatWindowMessageDelegate);
        
        // if (_windowRefs.Any())
        //     app.MainWindow.SetParent(_windowRefs.First(wr => wf.Type == WindowType.Main).Window)
        
        if (appHook != null)
            appHook(app);

        var settingsProvider = app.Services.GetService<SettingsProvider<Settings>>();
        var tokenProvider = app.Services.GetService<ITokenProvider>();
        
        tokenProvider?.SetToken(settingsProvider.CurrentValue.Authentication.AccessToken);

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
        var windowRef = _windowRefs.FirstOrDefault(wf => wf.Type == type);
        
        // if (windowRef == null)
        //    windowRef.Window.Focus();
    }
    
    static PhotinoBlazorApp RegisterMediaHandler(this PhotinoBlazorApp app)
    {
        var settingsProvider = app.Services.GetService<SettingsProvider<Settings>>();
        
        app.MainWindow.RegisterCustomSchemeHandler("media",
            (object sender, string scheme, string url, out string contentType) =>
            {
                try
                {
                    var uri = new Uri(url);

                    var query = HttpUtility.ParseQueryString(uri.Query);

                    var id = query["id"];
                    var fileId = query["fileId"];
                    var crc32 = query["crc32"];
                    var mime = query["mime"];

                    contentType = mime;

                    var filePath = Path.Combine(settingsProvider.CurrentValue.Media.StoragePath, $"{fileId}-{crc32}");

                    if (File.Exists(filePath))
                        return new FileStream(filePath, FileMode.Open, FileAccess.Read);
                    else
                        return null;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Unable to load media file from local disk {FileUrl}", url);

                    contentType = "";
                }

                return null;
            });

        return app;
    }
    
    static void ChatWindowMessageDelegate(object sender, string message)
    {
        if (message == "openChat")
        {
            CreateWindow<UI.App_Chat>(new WindowOptions()
            {
                Title = "LANCommander Chat",
                Type = WindowType.Chat,
            }, null, null, new string[] { });
        }
    }

    internal static void CloseAllWindows()
    {
        foreach (var windowRef in _windowRefs)
            windowRef.Window.Close();
    }
}