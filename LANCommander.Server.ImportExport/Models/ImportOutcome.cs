namespace LANCommander.Server.ImportExport.Models;

/// <summary>What happened to a queued record on one attempt to import it.</summary>
public enum ImportOutcome
{
    Imported,

    /// <summary>Waiting on the record it belongs to; tried again once that has been imported.</summary>
    Deferred,

    /// <summary>Can never be imported here, e.g. a save whose owner isn't on this server.</summary>
    Skipped,

    /// <summary>Its import failed; reported and left out.</summary>
    Failed,
}
