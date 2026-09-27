using LANCommander.Testing.Visual;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// Captures a page (or one element of it) and compares it with its committed baseline in
/// <c>Baselines/</c>. Run with <c>UPDATE_VISUAL_BASELINES=1</c> to accept captures as baselines.
/// </summary>
/// <remarks>
/// Baselines are generated on Linux CI (the LANCommander.Server.UI.Tests.UpdateBaselines workflow);
/// font rasterization differs between platforms, so a Windows or macOS capture will not match them.
/// </remarks>
public static class VisualAssert
{
    public static VisualBaselineStore Store { get; } = VisualBaselineStore.ForAssembly(typeof(VisualAssert).Assembly);

    /// <summary>Hides the text caret and stops transitions, which screenshots cannot otherwise settle.</summary>
    private const string StabilizingStyles = """
        *, *::before, *::after {
            caret-color: transparent !important;
            transition: none !important;
            animation-duration: 0s !important;
            animation-delay: 0s !important;
        }
        """;

    public static async Task MatchesBaselineAsync(IPage page, string name, ILocator? element = null)
    {
        await SettleAsync(page);

        var path = Store.GetScreenshotPath(name);

        if (element != null)
        {
            await element.ScreenshotAsync(new LocatorScreenshotOptions
            {
                Path = path,
                Animations = ScreenshotAnimations.Disabled,
                Caret = ScreenshotCaret.Hide,
            });
        }
        else
        {
            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                FullPage = true,
                Animations = ScreenshotAnimations.Disabled,
                Caret = ScreenshotCaret.Hide,
            });
        }

        var (result, message) = Store.Verify(name);

        Assert.True(result.Passed, message);
    }

    /// <summary>Waits until nothing that affects pixels is still in flight.</summary>
    public static async Task SettleAsync(IPage page)
    {
        await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = StabilizingStyles });

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Web fonts load with font-display: swap; capturing before they arrive shows fallback glyphs
        await page.EvaluateAsync("() => document.fonts.ready");

        // Images still decoding render as blank boxes
        await page.EvaluateAsync("""
            () => Promise.all([...document.images]
                .filter(img => !img.complete)
                .map(img => new Promise(resolve => { img.onload = img.onerror = resolve; })))
            """);

        // Let Blazor apply any render batch triggered by the loads above
        await page.EvaluateAsync("() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))");
    }
}
