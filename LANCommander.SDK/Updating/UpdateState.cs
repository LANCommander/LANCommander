namespace LANCommander.SDK.Updating;

/// <summary>Where an update is in its life, as recorded in its <see cref="UpdateJournal"/>.</summary>
public enum UpdateState
{
    /// <summary>The package is extracted to the staging directory; the installation is untouched.</summary>
    Staged,

    /// <summary>Files are being swapped. A journal left in this state means the swap was cut short.</summary>
    Applying,

    /// <summary>Every file is swapped; the updated process has not yet reported a healthy start.</summary>
    Applied,

    /// <summary>The updated process started and reported itself healthy.</summary>
    Confirmed,

    /// <summary>The update is in place, but some of the replaced files could not be deleted yet.</summary>
    Completed,

    /// <summary>The swap was undone and the previous files are back in place.</summary>
    RolledBack,
}
