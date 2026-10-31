using LANCommander.Server.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// <see cref="IRecurringJobScheduler"/> as plugins use it, against the server's own Hangfire, and the
/// runner that hands each job to its plugin's handler.
/// </summary>
[Collection("Application")]
public class RecurringJobSchedulerTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public void AJobIsScheduledChangedAndRemoved()
    {
        var scheduler = GetService<IRecurringJobScheduler>();
        var jobId = $"tests.{Guid.NewGuid():N}";

        scheduler.AddOrUpdate(jobId, "tests.handler", "argument", "0 2 * * *", TimeZoneInfo.Utc);

        var job = scheduler.Get(jobId);

        job.ShouldNotBeNull();
        job.CronExpression.ShouldBe("0 2 * * *");
        job.NextExecution.ShouldNotBeNull();
        job.NextExecution.Value.Hour.ShouldBe(2);

        scheduler.AddOrUpdate(jobId, "tests.handler", "argument", "*/15 * * * *", TimeZoneInfo.Utc);

        scheduler.Get(jobId)!.CronExpression.ShouldBe("*/15 * * * *");

        scheduler.Remove(jobId);

        scheduler.Get(jobId).ShouldBeNull();
    }

    [Theory]
    [InlineData("0 * * * *", true)]
    [InlineData("*/15 * * * *", true)]
    [InlineData("0 2 * * 1-5", true)]
    [InlineData("30 0 2 * * *", true)]
    [InlineData("", false)]
    [InlineData("every hour", false)]
    [InlineData("61 * * * *", false)]
    [InlineData("* * *", false)]
    public void CronExpressionsAreChecked(string cronExpression, bool valid) =>
        GetService<IRecurringJobScheduler>().IsValidCronExpression(cronExpression).ShouldBe(valid);

    [Fact]
    public void AnInvalidScheduleIsRefused() =>
        Should.Throw<ArgumentException>(() => GetService<IRecurringJobScheduler>().AddOrUpdate("tests.invalid", "tests.handler", "", "every hour"));

    [Fact]
    public void TriggeringAJobThatIsntThereDoesNothing() =>
        Should.NotThrow(() => GetService<IRecurringJobScheduler>().Trigger($"tests.{Guid.NewGuid():N}"));

    [Fact]
    public async Task TheRunnerHandsTheJobToTheHandlerNamedWithItsArgument()
    {
        var handler = new RecordingHandler("tests.recording");
        var runner = Runner(handler, new RecordingHandler("tests.other"));

        await runner.ExecuteAsync("tests.job", "tests.recording", "remote-1", CancellationToken.None);

        handler.Arguments.ShouldBe(["remote-1"]);
    }

    [Fact]
    public async Task AJobWhosePluginIsGoneFailsSayingSo()
    {
        var runner = Runner();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => runner.ExecuteAsync("tests.job", "tests.missing", "", CancellationToken.None));

        exception.Message.ShouldContain("no handler named tests.missing");
    }

    static PluginRecurringJobRunner Runner(params IRecurringJobHandler[] handlers)
    {
        var services = new ServiceCollection();

        foreach (var handler in handlers)
            services.AddSingleton(handler);

        return new PluginRecurringJobRunner(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PluginRecurringJobRunner>.Instance);
    }

    sealed class RecordingHandler(string name) : IRecurringJobHandler
    {
        public List<string> Arguments { get; } = [];

        public string Name => name;

        public Task ExecuteAsync(string argument, CancellationToken cancellationToken)
        {
            Arguments.Add(argument);

            return Task.CompletedTask;
        }
    }
}
