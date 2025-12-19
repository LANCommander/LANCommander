#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LANCommander.SDK.Migrations;

/// <summary>
/// Represents the outcome of a migration execution attempt.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MigrationStatus
{
    /// <summary>
    /// Migration executed successfully.
    /// </summary>
    Executed,

    /// <summary>
    /// Migration was skipped because ShouldExecuteAsync returned false.
    /// </summary>
    SkippedNotApplicable,

    /// <summary>
    /// Migration was skipped because it was already executed previously.
    /// </summary>
    SkippedAlreadyExecuted,

    /// <summary>
    /// Migration was skipped because pre-checks failed.
    /// </summary>
    SkippedPreChecksFailed,

    /// <summary>
    /// Migration failed during execution.
    /// </summary>
    Failed
}

/// <summary>
/// Represents a single migration history entry with metadata about the execution.
/// </summary>
public class MigrationHistoryEntry
{
    /// <summary>
    /// The fully qualified type name of the migration.
    /// </summary>
    public string MigrationName { get; set; } = string.Empty;

    /// <summary>
    /// The version of the migration.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The outcome of the migration attempt.
    /// </summary>
    public MigrationStatus Status { get; set; }

    /// <summary>
    /// When the migration was attempted.
    /// </summary>
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>
    /// How long the migration took to execute (in milliseconds).
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Whether ShouldExecuteAsync returned true.
    /// </summary>
    public bool ShouldExecute { get; set; }

    /// <summary>
    /// Whether PerformPreChecksAsync returned true (null if not evaluated).
    /// </summary>
    public bool? PreChecksPassed { get; set; }

    /// <summary>
    /// Error message if the migration failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Stack trace if the migration failed.
    /// </summary>
    public string? StackTrace { get; set; }

    /// <summary>
    /// The application version that ran this migration.
    /// </summary>
    public string? ApplicationVersion { get; set; }

    /// <summary>
    /// The name of the application that ran this migration.
    /// </summary>
    public string? Application { get; set; }

    /// <summary>
    /// The machine name where the migration was executed.
    /// </summary>
    public string? MachineName { get; set; }
}

/// <summary>
/// Represents the complete migration history for the application.
/// </summary>
public class MigrationHistory
{
    /// <summary>
    /// Schema version for the migration history file format.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// When this history file was last updated.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// Collection of all migration history entries.
    /// </summary>
    public List<MigrationHistoryEntry> Entries { get; set; } = new();
}
