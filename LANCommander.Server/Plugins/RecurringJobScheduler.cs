using Hangfire;
using Hangfire.Common;
using Hangfire.Storage;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Gives plugins the server's Hangfire recurring jobs. Every plugin job runs
/// <see cref="PluginRecurringJobRunner"/>, a server type, which finds the plugin's handler by name:
/// Hangfire stores jobs by type name and couldn't load a type from a plugin's load context.
/// </summary>
internal sealed class RecurringJobScheduler(IRecurringJobManager manager, JobStorage storage) : IRecurringJobScheduler
{
    /// <summary>Keeps plugin jobs apart from the server's own, which are named after their types.</summary>
    internal const string JobIdPrefix = "plugin:";

    public void AddOrUpdate(string jobId, string handler, string argument, string cronExpression, TimeZoneInfo? timeZone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handler);

        if (!IsValidCronExpression(cronExpression))
            throw new ArgumentException($"\"{cronExpression}\" isn't a cron expression, such as \"0 * * * *\" for every hour.", nameof(cronExpression));

        var job = Job.FromExpression<PluginRecurringJobRunner>(runner =>
            runner.ExecuteAsync(jobId, handler, argument ?? "", CancellationToken.None));

        manager.AddOrUpdate(JobIdPrefix + jobId, job, cronExpression.Trim(), new RecurringJobOptions
        {
            TimeZone = timeZone ?? TimeZoneInfo.Local,
        });
    }

    public void Remove(string jobId) => manager.RemoveIfExists(JobIdPrefix + jobId);

    public void Trigger(string jobId)
    {
        if (Get(jobId) != null)
            manager.Trigger(JobIdPrefix + jobId);
    }

    public RecurringJobInfo? Get(string jobId)
    {
        using var connection = storage.GetConnection();

        var job = connection.GetRecurringJobs([JobIdPrefix + jobId]).FirstOrDefault(j => !j.Removed);

        return job == null ? null : new RecurringJobInfo(jobId, job.Cron, job.NextExecution, job.LastExecution);
    }

    public bool IsValidCronExpression(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression))
            return false;

        var fields = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Hangfire reads six fields as seconds first, as Cronos does with IncludeSeconds
        if (fields is not (5 or 6))
            return false;

        try
        {
            Cronos.CronExpression.Parse(cronExpression.Trim(), fields == 6 ? Cronos.CronFormat.IncludeSeconds : Cronos.CronFormat.Standard);

            return true;
        }
        catch (Cronos.CronFormatException)
        {
            return false;
        }
    }
}

/// <summary>Runs a plugin's recurring job: the method every plugin job in Hangfire's storage names.</summary>
public sealed class PluginRecurringJobRunner(IServiceScopeFactory scopes, ILogger<PluginRecurringJobRunner> logger)
{
    /// <remarks>
    /// Not retried: Hangfire would otherwise rerun a failing job up to ten times over the following
    /// hours, on top of its schedule.
    /// </remarks>
    [AutomaticRetry(Attempts = 0)]
    [JobDisplayName("Plugin job {0}")]
    public async Task ExecuteAsync(string jobId, string handler, string argument, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();

        var match = scope.ServiceProvider.GetServices<IRecurringJobHandler>()
            .Where(h => string.Equals(h.Name, handler, StringComparison.Ordinal))
            .ToList();

        if (match.Count != 1)
            throw new InvalidOperationException(match.Count == 0
                ? $"No plugin handles the job {jobId}: there's no handler named {handler}. Was its plugin removed?"
                : $"The job {jobId} can't run: {match.Count} plugins have a handler named {handler}.");

        logger.LogDebug("Running plugin job {JobId} with {Handler}", jobId, handler);

        await match[0].ExecuteAsync(argument, cancellationToken);
    }
}
