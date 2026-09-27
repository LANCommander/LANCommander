using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Parameters every control accepts: extra classes, inline style, and any other HTML attributes,
/// which land on the control's outermost element.
/// </summary>
public abstract class ControlBase : ComponentBase
{
    [Parameter] public string? Class { get; set; }

    [Parameter] public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
}
