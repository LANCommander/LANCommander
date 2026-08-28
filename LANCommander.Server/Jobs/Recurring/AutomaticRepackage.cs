using Hangfire;
using LANCommander.SDK.Enums;
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
        if (!settingsProvider.CurrentValue.Server.Scripts.EnableAutomaticRepackaging)
        {
            logger.LogDebug("The automatic repackaging job attempted to execute, but automatic repackaging is disabled");
            return;
        }
        
        logger.LogInformation("Starting automatic repackaging job");
        
        var repackageScripts = await scriptService.GetAsync(s => s.Type == ScriptType.Package);
        
        logger.LogInformation("Found {ScriptCount} packaging scripts}", repackageScripts.Count);

        foreach (var script in repackageScripts)
        {
            try
            {
                if (script.GameId.HasValue)
                    await gameService.PackageAsync(script.GameId.Value);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not automatically run packaging script with ID {ScriptId}", script.Id);
            }
        }
    }
}