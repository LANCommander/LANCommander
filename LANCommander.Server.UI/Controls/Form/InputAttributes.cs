using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LANCommander.Server.UI.Controls;

/// <summary>Builds the attribute dictionary passed to a wrapped Radzen input.</summary>
internal static class InputAttributes
{
    public static Dictionary<string, object> Build<TValue>(FormInputBase<TValue> input, bool autoFocus, EventCallback onEnter, string? cssClass)
    {
        var attributes = input.AdditionalAttributes?.ToDictionary() ?? new Dictionary<string, object>();

        if (cssClass != null)
            attributes["class"] = cssClass;

        if (autoFocus)
            attributes["autofocus"] = true;

        if (onEnter.HasDelegate)
        {
            attributes["onkeydown"] = EventCallback.Factory.Create<KeyboardEventArgs>(input, async args =>
            {
                if (args.Key is "Enter" or "NumpadEnter")
                    await onEnter.InvokeAsync();
            });
        }

        return attributes;
    }
}
