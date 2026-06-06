using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

// This assembly-level attribute tells Avalonia.Headless.XUnit which AppBuilder to use.
// All [AvaloniaFact] and [AvaloniaTheory] tests in this project run under this headless app.
[assembly: AvaloniaTestApplication(typeof(LANCommander.Launcher.Tests.TestAppBuilder))]

namespace LANCommander.Launcher.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            // UseHeadlessDrawing = false → real Skia rendering → pixel-accurate screenshots.
            // UseSkia() registers the Skia rendering backend (IFontManagerImpl etc.).
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont();
}
