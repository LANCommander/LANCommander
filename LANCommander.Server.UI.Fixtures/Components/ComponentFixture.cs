using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Fixtures.Components;

/// <summary>
/// One LANCommander.Server.UI control (or small composition of controls) rendered in a known state.
/// </summary>
/// <param name="Name">
/// Unique <c>Control.State</c> name, e.g. <c>Button.Variants</c>. Used in the <c>/_fixtures/{Name}</c>
/// route and as the visual baseline file name, so keep it stable once a baseline exists.
/// </param>
/// <param name="Description">What the fixture shows, for the gallery index.</param>
/// <param name="Content">The markup to render.</param>
public sealed record ComponentFixture(string Name, string Description, RenderFragment Content)
{
    /// <summary>Width of the frame the fixture renders in. Height follows the content.</summary>
    public int Width { get; init; } = 480;

    /// <summary>
    /// The controls this fixture exercises. Every public Control must be covered by at least one
    /// fixture; a test enforces it.
    /// </summary>
    public IReadOnlyList<Type> Covers { get; init; } = [];

    /// <summary>
    /// Capture the whole viewport instead of just the fixture frame, for overlays (dialogs,
    /// notifications, popovers) that render outside it.
    /// </summary>
    public bool FullPage { get; init; }

    /// <summary>A CSS selector the visual test hovers before capturing, e.g. to open a tooltip.</summary>
    public string? Hover { get; init; }

    /// <summary>A CSS selector the visual test clicks before capturing, e.g. to open a popover.</summary>
    public string? Click { get; init; }

    /// <summary>A CSS selector that must be visible before capturing, e.g. the overlay a fixture opens.</summary>
    public string? WaitFor { get; init; }

    /// <summary>The control name, the part of <see cref="Name"/> before the first dot.</summary>
    public string Group => Name.Split('.')[0];
}
