using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Controls;

public enum DialogSize
{
    /// <summary>400px, for confirmations and short prompts.</summary>
    Small,
    /// <summary>600px, the default.</summary>
    Medium,
    /// <summary>800px, for forms with several fields.</summary>
    Large,
    /// <summary>1100px, for editors and pickers.</summary>
    ExtraLarge,
    /// <summary>Nearly the whole viewport.</summary>
    Full,
}

/// <summary>How a dialog's window and footer look. All optional.</summary>
public sealed record DialogSettings
{
    public DialogSize Size { get; init; } = DialogSize.Medium;

    public string OkText { get; init; } = "OK";

    public string CancelText { get; init; } = "Cancel";

    /// <summary>The OK button performs a destructive action and is drawn as such.</summary>
    public bool Danger { get; init; }

    /// <summary>Only an OK button, for dialogs that just inform.</summary>
    public bool NoCancel { get; init; }

    /// <summary>No footer at all; the dialog closes itself (or through the close button).</summary>
    public bool NoFooter { get; init; }

    /// <summary>Prevents closing through the close button or Escape, for work that must finish.</summary>
    public bool NotClosable { get; init; }

    /// <summary>
    /// Rendered in the title bar after the title: chips (<c>&lt;Tag Caps&gt;Cover&lt;/Tag&gt;</c>), a
    /// name, a status. Content that follows the dialog's own state belongs in its
    /// <see cref="Dialog{TOptions,TResult}.TitleContent"/> instead, which re-renders with it.
    /// </summary>
    public RenderFragment? TitleContent { get; init; }

    /// <summary>Muted text at the end of the title, e.g. the game the dialog edits.</summary>
    public string? Subtitle { get; init; }

    /// <summary>Sets <see cref="Subtitle"/> in 12px mono, for paths, sizes and counts.</summary>
    public bool MonoSubtitle { get; init; }

    /// <summary>A 12px note at the start of the footer, beside the buttons, e.g. what OK will do.</summary>
    public string? FooterNote { get; init; }

    /// <summary>
    /// A full-screen picker or editor (media, files, scripts): the window is inset 20px from the
    /// viewport whatever <see cref="Size"/> says, the body has no padding so the dialog's own
    /// toolbars run edge to edge, and the footer is a 56px well with 34px buttons.
    /// </summary>
    public bool Picker { get; init; }

    /// <summary>With <see cref="Picker"/>: a 48px footer, for editors whose status bar shares it.</summary>
    public bool CompactFooter { get; init; }

    internal string? Height => Picker ? "calc(100vh - 40px)" : null;

    internal string Width => Picker ? "calc(100vw - 40px)" : Size switch
    {
        DialogSize.Small => "400px",
        DialogSize.Large => "800px",
        DialogSize.ExtraLarge => "1100px",
        DialogSize.Full => "calc(100vw - 64px)",
        _ => "600px",
    };
}
