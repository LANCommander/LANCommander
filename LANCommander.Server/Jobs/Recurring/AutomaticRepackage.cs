using Hangfire;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services;

namespace LANCommander.Server.Jobs.Recurring;

public sealed class AutomaticRepackage(
    GameService gameService,
    ScriptService scriptService,
    ILogger<AutomaticRepackage> logger) : BaseRecurringJob(logger)
{
    public override string CronExpression
    {
        get
        {
            var settings = SettingService.GetSettings();

            var minutes = settings.Scripts.RepackageEvery % 60;
            var hours = settings.Scripts.RepackageEvery / 60;
            var days = hours % 24;

            var minuteExpression = minutes > 0 ? $"*/{minutes}" : "*";
            var hourExpression = hours > 0 ? $"*/{hours}" : "*";
            var dayExpression = days > 0 ? $"*/{days}" : "*";

            return $"{minuteExpression} {hourExpression} {dayExpression} * *";
        }
    }

    public override async Task ExecuteAsync()
    {
        var settings = SettingService.GetSettings();

        if (!settings.Scripts.EnableAutomaticRepackaging)
        {
            logger.LogWarning("The automatic repackaging job attempted to execute, but automatic repackaging is disabled");
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