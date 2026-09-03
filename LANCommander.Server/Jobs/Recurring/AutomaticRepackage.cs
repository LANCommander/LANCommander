using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Recurring;

public sealed class AutomaticRepackage(
    SettingsProvider<Settings.Settings> settingsProvider,
    GameService gameService,
    ScriptService scriptService,
    ILogger<AutomaticRepackage> logger) : BaseRecurringJob(logger)
{
    public override string CronExpression
    {
        get
        {
            var hours = Math.Max(1, settingsProvider.CurrentValue.Server.Scripts.RepackageEvery);

            if (hours < 24)
                return $"0 */{hours} * * *";

            return $"0 0 */{Math.Clamp(hours / 24, 1, 31)} * *";
        }
    }

    public override async Task ExecuteAsync()
    {
        // This job is not allowed to throw: an unhandled exception here is picked up by Hangfire's
        // AutomaticRetryAttribute, which retries the *entire* job up to 10 times with an exponentially
        // growing delay (over an hour by the later attempts). A single bad game/script should never be
        // able to take down the whole recurring job, so every failure path below is caught and logged
        // instead of being allowed to bubble out.
        try
        {
            if (!settingsProvider.CurrentValue.Server.Scripts.EnableAutomaticRepackaging)
            {
                logger.LogDebug("The automatic repackaging job attempted to execute, but automatic repackaging is disabled");
                return;
            }

            logger.LogInformation("Starting automatic repackaging job");

            ICollection<Script>? repackageScripts;

            try
            {
                repackageScripts = await scriptService.GetAsync(s => s.Type == ScriptType.Package);

                logger.LogInformation("Found {ScriptCount} packaging scripts", repackageScripts.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not retrieve packaging scripts for automatic repackaging");
                return;
            }

            var succeeded = 0;
            var failed = 0;

            foreach (var script in repackageScripts)
            {
                try
                {
                    if (script.GameId.HasValue)
                    {
                        await gameService.PackageAsync(script.GameId.Value);
                        succeeded++;
                    }
                }
                catch (Exception ex)
                {
                    failed++;

                    logger.LogError(
                        ex,
                        "Could not automatically run packaging script with ID {ScriptId} for game {GameId}: {ExceptionType} - {ExceptionMessage}",
                        script.Id,
                        script.GameId,
                        ex.GetType().Name,
                        ex.Message);
                }
            }

            logger.LogInformation(
                "Finished automatic repackaging job: {SucceededCount} succeeded, {FailedCount} failed",
                succeeded,
                failed);
        }
        catch (Exception ex)
        {
            // Catch-all so an unexpected failure (e.g. a settings/service issue outside the per-script
            // loop) is logged clearly instead of triggering Hangfire's long automatic-retry backoff.
            logger.LogError(ex, "Automatic repackaging job failed unexpectedly: {ExceptionType} - {ExceptionMessage}", ex.GetType().Name, ex.Message);
        }
    }
}