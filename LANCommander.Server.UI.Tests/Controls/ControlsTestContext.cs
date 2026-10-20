using Bunit;
using LANCommander.Server.UI.Extensions;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>
/// Base class for LANCommander.Server.UI control tests. Unlike the page tests this needs no server,
/// database or seeded data: just the UI library's own services, rendered in-process.
/// </summary>
public abstract class ControlsTestContext : BunitContext
{
    protected ControlsTestContext()
    {
        // Radzen components call into Radzen.Blazor.js for focus, measuring and popups; loose mode
        // answers every call with a default so rendering proceeds without a browser.
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Radzen charts ask the browser for their size before drawing; loose mode would return null
        JSInterop.Setup<Radzen.Blazor.Rendering.Rect>("Radzen.createChart", _ => true)
            .SetResult(new Radzen.Blazor.Rendering.Rect { Width = 600, Height = 300 });

        Services.AddLANCommanderServerUI();
    }
}
