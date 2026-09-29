using Bunit;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Controls;

public class OverlayTests : ControlsTestContext
{
    [Fact]
    public void Dropdown_Popup_IsAbsolutelyPositioned()
    {
        // Radzen's JS moves the popup to <body> and sets left/top from a viewport-clamped rect; without
        // position:absolute the panel renders static at the page bottom and off-screen (see the
        // reference-radzen-popup-positioning note). The wrapper must keep the popup absolutely
        // positioned so the runtime clamp in ThemeScripts can place it on-screen.
        var cut = Render<Dropdown>(p =>
        {
            p.Add(x => x.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<button type=\"button\">Open</button>")));
            p.Add(x => x.Overlay, (RenderFragment)(b => b.AddMarkupContent(0, "<div class=\"lc-menu-list\">Item</div>")));
        });

        Assert.Contains("position: absolute", cut.Markup);
    }

    [Fact]
    public void Dropdown_RendersTriggerAndOverlayContent()
    {
        var cut = Render<Dropdown>(p =>
        {
            p.Add(x => x.ChildContent, (RenderFragment)(b => b.AddMarkupContent(0, "<button type=\"button\">Open</button>")));
            p.Add(x => x.Overlay, (RenderFragment)(b => b.AddMarkupContent(0, "<div class=\"lc-menu-list\">Item</div>")));
        });

        Assert.Contains("lc-dropdown-trigger", cut.Markup);
        Assert.Contains("lc-dropdown", cut.Markup);
    }
}
