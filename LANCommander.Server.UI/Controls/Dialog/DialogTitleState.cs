using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Controls.Internal;

/// <summary>
/// Joins a dialog's title bar, which Radzen renders outside the dialog's own component tree, to the
/// frame inside it: the frame supplies the dialog's live title content and asks the title to redraw
/// whenever the dialog body renders.
/// </summary>
public sealed class DialogTitleState
{
    internal DialogTitleState(string title, DialogSettings settings)
    {
        Title = title;
        Settings = settings;
    }

    internal string Title { get; }

    internal DialogSettings Settings { get; }

    /// <summary>The open dialog's own title content; set by the frame once the dialog exists.</summary>
    internal Func<RenderFragment?>? DialogContent { get; set; }

    /// <summary>Redraws the title; set by the title slot when it renders.</summary>
    internal Action? Refresh { get; set; }
}
