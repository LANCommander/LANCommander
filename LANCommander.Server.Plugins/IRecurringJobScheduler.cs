namespace LANCommander.Server.Plugins;

/// <summary>
/// Runs plugin work on a cron schedule through the server's background job processor, alongside the
/// server's own recurring jobs and on its dashboard. Provided by the server; inject it anywhere, it's a
/// singleton.
/// </summary>
/// <remarks>
/// <para>
/// A job names a <see cref="IRecurringJobHandler"/> the plugin registers, and an argument passed to it,
/// so one handler can serve many jobs, such as one per configured item. Schedules aren't kept across
/// restarts: add them from <c>IPlugin.InitializeAsync</c>, and again whenever they change.
/// </para>
/// <para>
/// Job ids and handler names are shared by every plugin, so start them with the plugin's id.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// scheduler.AddOrUpdate("acme.backup.nightly", BackupHandler.Name, "", "0 2 * * *");
/// </code>
/// </example>
public interface IRecurringJobScheduler
{
    /// <summary>Adds a recurring job, or changes the one with the same id.</summary>
    /// <param name="jobId">Identifies the job; start it with the plugin's id.</param>
    /// <param name="handler">The <see cref="IRecurringJobHandler.Name"/> of the handler that runs it.</param>
    /// <param name="argument">Passed to the handler, e.g. the id of the item the job is for.</param>
    /// <param name="cronExpression">
    /// When it runs, as a five-field cron expression (minute, hour, day of month, month, day of week),
    /// or six with seconds first.
    /// </param>
    /// <param name="timeZone">The time zone the expression is read in. Defaults to the server's.</param>
    /// <exception cref="ArgumentException">The cron expression or time zone isn't valid.</exception>
    void AddOrUpdate(string jobId, string handler, string argument, string cronExpression, TimeZoneInfo? timeZone = null);

    /// <summary>Removes a recurring job. Does nothing when there's no such job.</summary>
    void Remove(string jobId);

    /// <summary>Runs a recurring job now, outside its schedule. Does nothing when there's no such job.</summary>
    void Trigger(string jobId);

    /// <summary>The plugin recurring job with the given id, or null.</summary>
    RecurringJobInfo? Get(string jobId);

    /// <summary>Whether <paramref name="cronExpression"/> is one <see cref="AddOrUpdate"/> accepts.</summary>
    bool IsValidCronExpression(string cronExpression);
}

/// <summary>
/// Does the work of recurring jobs added through <see cref="IRecurringJobScheduler"/>. Register an
/// implementation in <c>IPlugin.ConfigureServices</c>; it's resolved from a fresh scope for every run.
/// </summary>
/// <remarks>
/// A run that throws is recorded as failed on the server's dashboard and isn't retried; the job runs
/// again at its next scheduled time.
/// </remarks>
public interface IRecurringJobHandler
{
    /// <summary>Identifies the handler to <see cref="IRecurringJobScheduler.AddOrUpdate"/>; start it with the plugin's id.</summary>
    string Name { get; }

    /// <summary>Runs the job.</summary>
    /// <param name="argument">The argument the job was added with.</param>
    /// <param name="cancellationToken">Cancelled when the server shuts down.</param>
    Task ExecuteAsync(string argument, CancellationToken cancellationToken);
}

/// <summary>A plugin recurring job, from <see cref="IRecurringJobScheduler.Get"/>.</summary>
/// <param name="JobId">The id it was added with.</param>
/// <param name="CronExpression">Its schedule.</param>
/// <param name="NextExecution">When it next runs, in UTC, if it's scheduled to.</param>
/// <param name="LastExecution">When it last ran, in UTC, if it has since the server started.</param>
public sealed record RecurringJobInfo(string JobId, string CronExpression, DateTime? NextExecution, DateTime? LastExecution);
