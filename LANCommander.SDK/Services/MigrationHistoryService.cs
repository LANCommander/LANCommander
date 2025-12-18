#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LANCommander.SDK.Migrations;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

/// <summary>
/// Service for tracking and persisting migration history to a JSON file.
/// </summary>
public class MigrationHistoryService(ILogger<MigrationHistoryService> logger)
{
    private const string HistoryFileName = "migration-history.json";

    private readonly string _historyFilePath = AppPaths.GetConfigPath(HistoryFileName);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private MigrationHistory? _history;

    /// <summary>
    /// Loads the migration history from disk.
    /// </summary>
    public async Task<MigrationHistory> LoadHistoryAsync()
    {
        if (_history is not null)
            return _history;

        if (!File.Exists(_historyFilePath))
        {
            logger.LogInformation("No migration history file found at {Path}, creating new history", _historyFilePath);
            _history = new MigrationHistory();
            return _history;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_historyFilePath);
            _history = JsonSerializer.Deserialize<MigrationHistory>(json, _jsonOptions) ?? new MigrationHistory();
            logger.LogInformation("Loaded migration history with {Count} entries", _history.Entries.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load migration history from {Path}, starting with empty history", _historyFilePath);
            _history = new MigrationHistory();
        }

        return _history;
    }

    /// <summary>
    /// Saves the migration history to disk.
    /// </summary>
    public async Task SaveHistoryAsync()
    {
        if (_history is null)
            return;

        try
        {
            _history.LastUpdated = DateTimeOffset.UtcNow;
            var json = JsonSerializer.Serialize(_history, _jsonOptions);

            var directory = Path.GetDirectoryName(_historyFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(_historyFilePath, json);
            logger.LogDebug("Saved migration history to {Path}", _historyFilePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save migration history to {Path}", _historyFilePath);
        }
    }

    /// <summary>
    /// Checks if a migration has already been successfully executed.
    /// </summary>
    public async Task<bool> HasMigrationBeenExecutedAsync(IMigration migration)
    {
        var history = await LoadHistoryAsync();
        var migrationName = migration.GetType().FullName ?? migration.GetType().Name;

        return history.Entries.Any(e =>
            e.MigrationName == migrationName &&
            e.Status == MigrationStatus.Executed);
    }

    /// <summary>
    /// Records the result of a migration attempt.
    /// </summary>
    public async Task RecordMigrationAsync(
        IMigration migration,
        MigrationStatus status,
        long durationMs,
        bool shouldExecute,
        bool? preChecksPassed = null,
        Exception? exception = null)
    {
        var history = await LoadHistoryAsync();

        var entry = new MigrationHistoryEntry
        {
            MigrationName = migration.GetType().FullName ?? migration.GetType().Name,
            Version = migration.Version.ToString(),
            Status = status,
            ExecutedAt = DateTimeOffset.UtcNow,
            DurationMs = durationMs,
            ShouldExecute = shouldExecute,
            PreChecksPassed = preChecksPassed,
            ErrorMessage = exception?.Message,
            StackTrace = exception?.StackTrace,
            ApplicationVersion = GetApplicationVersion(),
            MachineName = Environment.MachineName
        };

        history.Entries.Add(entry);

        await SaveHistoryAsync();

        logger.LogInformation(
            "Recorded migration {MigrationName} with status {Status}",
            entry.MigrationName,
            entry.Status);
    }

    /// <summary>
    /// Gets all history entries for a specific migration.
    /// </summary>
    public async Task<IReadOnlyList<MigrationHistoryEntry>> GetMigrationHistoryAsync(string migrationName)
    {
        var history = await LoadHistoryAsync();
        return [.. history.Entries
            .Where(e => e.MigrationName == migrationName)
            .OrderByDescending(e => e.ExecutedAt)];
    }

    /// <summary>
    /// Gets all successfully executed migrations.
    /// </summary>
    public async Task<IReadOnlyList<MigrationHistoryEntry>> GetExecutedMigrationsAsync()
    {
        var history = await LoadHistoryAsync();
        return [.. history.Entries
            .Where(e => e.Status == MigrationStatus.Executed)
            .OrderBy(e => e.ExecutedAt)];
    }

    private static string? GetApplicationVersion()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
