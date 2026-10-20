using LANCommander.SDK.Enums;

namespace LANCommander.Server.ImportExport.Models;

/// <summary>
/// Outcome of a complete (initialize -> prepare -> commit) import run.
/// </summary>
public class ImportRunResult
{
    /// <summary>
    /// Id of the record that was created or updated by the import.
    /// </summary>
    public Guid RecordId { get; set; }

    /// <summary>
    /// The kind of manifest that was detected in the archive.
    /// </summary>
    public ManifestType ManifestType { get; set; }

    /// <summary>
    /// Number of records that were imported.
    /// </summary>
    public int ImportedCount { get; set; }
}
