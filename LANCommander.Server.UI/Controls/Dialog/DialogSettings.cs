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

    internal string Width => Size switch
    {
        DialogSize.Small => "400px",
        DialogSize.Large => "800px",
        DialogSize.ExtraLarge => "1100px",
        DialogSize.Full => "calc(100vw - 64px)",
        _ => "600px",
    };
}
