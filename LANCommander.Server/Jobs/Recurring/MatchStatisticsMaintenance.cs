using LANCommander.Server.Services.MatchStatistics;

namespace LANCommander.Server.Jobs.Recurring;

/// <summary>
/// Every five minutes: abandons matches that stopped reporting, removes expired and incomplete
/// attachments, and retries linking recent participants to users.
/// </summary>
public sealed class MatchStatisticsMaintenance(
    SettingsProvider<Settings.Settings> settingsProvider,
    MatchService matchService,
    MatchAttachmentService attachmentService,
    PlayerIdentityService identityService,
    ILogger<MatchStatisticsMaintenance> logger) : BaseRecurringJob(logger)
{
    public override string CronExpression => "*/5 * * * *";

    public override async Task ExecuteAsync()
    {
        // Never throws, so Hangfire doesn't retry the whole job; each step fails on its own
        var settings = settingsProvider.CurrentValue.Server.MatchStatistics;

        if (!settings.Enabled)
            return;

        if (settings.AbandonAfter > 0)
            await RunAsync("abandoning stale matches", () => matchService.AbandonStaleAsync(TimeSpan.FromMinutes(settings.AbandonAfter)));

        await RunAsync("removing expired match attachments", () => attachmentService.CleanUpAsync());

        await RunAsync("linking match participants", () => identityService.ResolveRecentAsync(DateTime.UtcNow.AddHours(-6)));
    }

    private async Task RunAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Match statistics maintenance failed {Step}", step);
        }
    }
}
