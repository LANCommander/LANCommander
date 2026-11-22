using LANCommander.SDK.Updating;
using LANCommander.Server.Services;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace LANCommander.Server.Startup;

/// <summary>
/// The server's side of a self-update: recovering from an interrupted one when starting, confirming
/// a healthy start after one, and swapping in a staged one once the server has stopped.
/// </summary>
public static class SelfUpdate
{
    // Migrations run before the server reports itself started, and can take a while on a large database
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Rolls back an update that was cut short while its files were being swapped, then restarts,
    /// since the files this process was started from may be a mix of two versions.
    /// </summary>
    public static WebApplication RecoverFromUpdate(this WebApplication app, string[] args)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        string journalDirectory;

        using (var scope = app.Services.CreateScope())
            journalDirectory = scope.ServiceProvider.GetRequiredService<UpdateService>().GetUpdateJournalDirectory();

        if (SelfUpdater.Recover(journalDirectory, logger) == UpdateRecoveryAction.RestartRequired)
        {
            logger.LogWarning("Restarting after rolling back an interrupted update");

            Restart(Environment.ProcessPath!, args);
        }

        // The previous version, started again after an update was undone, tells administrators why
        if (UpdateResult.Take(journalDirectory) is { } result)
            new ServerUpdateNotice { Version = result.Version, PreviousVersion = result.PreviousVersion, Error = result.Error }.Save(journalDirectory);

        // The updated server reports a healthy start once it's serving requests, then shows
        // administrators the release notes
        app.Lifetime.ApplicationStarted.Register(() => _ = Task.Run(async () =>
        {
            try
            {
                if (SelfUpdater.ConfirmStartup(journalDirectory, logger) is { } confirmed)
                    new ServerUpdateNotice { Version = confirmed.Version, PreviousVersion = confirmed.PreviousVersion }.Save(journalDirectory);

                await SelfUpdater.FinishAsync(journalDirectory, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not finish the update");
            }
        }));

        return app;
    }

    /// <summary>
    /// Run after the server has stopped. When an update was requested, swaps in its files and hands
    /// over to the updated server, rolling back if it doesn't start.
    /// </summary>
    public static async Task HandOverToUpdateAsync(this WebApplication app, string[] args)
    {
        if (ServerUpdateHandover.PendingJournalDirectory is not { } journalDirectory)
            return;

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var journal = UpdateJournal.Load(journalDirectory);

        if (journal?.State != UpdateState.Staged)
            return;

        var executable = Environment.ProcessPath!;

        try
        {
            await UpdateHandover.ApplyAsync(journal, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not apply update {Version}, starting the current version again", journal.Version);

            Fail(journal, $"The update's files could not be installed: {ex.Message}");
            Restart(executable, args);
            return;
        }

        // A service manager owns the process; exiting with an error has it start the updated files
        if (IsServiceManaged())
        {
            logger.LogInformation("Update {Version} applied, exiting so the service manager restarts the server", journal.Version);

            Environment.Exit(1);
        }

        var process = UpdateHandover.Start(journal, args, Environment.CurrentDirectory);

        if (await UpdateHandover.SuperviseAsync(journal, process, StartupTimeout, logger))
            return;

        var error = process.HasExited
            ? $"LANCommander Server {journal.Version} stopped while starting (exit code {process.ExitCode})."
            : $"LANCommander Server {journal.Version} did not finish starting within {StartupTimeout.TotalMinutes:0} minutes.";

        var restored = await UpdateHandover.RollbackAsync(journal, logger);

        logger.LogWarning("Update {Version} was rolled back, starting the current version again", journal.Version);

        Fail(journal, restored ? error : $"{error} Some of the previous version's files could not be restored; reinstall the server if it misbehaves.");
        Restart(executable, args);
    }

    /// <summary>Records why the update was undone, for the restarted server to show administrators.</summary>
    private static void Fail(UpdateJournal journal, string error)
    {
        try
        {
            new UpdateResult { Version = journal.Version, PreviousVersion = journal.PreviousVersion, Error = error }.Save(journal.Directory);
        }
        catch
        {
            // Administrators just won't see why
        }
    }

    private static void Restart(string executable, string[] args)
    {
        if (IsServiceManaged())
            Environment.Exit(1);

        UpdateHandover.StartExecutable(executable, args, Environment.CurrentDirectory);

        Environment.Exit(0);
    }

    private static bool IsServiceManaged() =>
        WindowsServiceHelpers.IsWindowsService() || SystemdHelpers.IsSystemdService();
}
