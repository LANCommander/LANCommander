using Microsoft.Extensions.Logging;
using Shouldly;

namespace LANCommander.Server.Tests.Startup;

[Collection("Application")]
public class LoggingConfigurationTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public void ApplicationCategoriesUseTheConfiguredMinimumLevel()
    {
        // Regression: AddFilter(Func<string, LogLevel, bool>) creates a LoggerFilterRule with a null
        // LogLevel. LoggerRuleSelector assigns that null straight into the effective minimum level,
        // discarding the configured floor for every category with no more specific rule -- which
        // enabled Trace globally and flooded the console with Hangfire's timer chatter.
        var logger = GetService<ILoggerFactory>().CreateLogger("LANCommander.Server.Services.GameService");

        logger.IsEnabled(LogLevel.Trace).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Debug).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
    }

    [Fact]
    public void HangfireCategoriesAreSuppressedBelowWarning()
    {
        var logger = GetService<ILoggerFactory>().CreateLogger("Hangfire.Server.ServerHeartbeatProcess");

        logger.IsEnabled(LogLevel.Trace).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Debug).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Information).ShouldBeFalse();
        logger.IsEnabled(LogLevel.Warning).ShouldBeTrue();
    }

    [Fact]
    public void PingMiddlewareCategoryIsSuppressedEntirely()
    {
        // IgnorePings defaults to true, so the middleware's category is filtered at LogLevel.None.
        var logger = GetService<ILoggerFactory>().CreateLogger(typeof(PingMiddleware).FullName!);

        logger.IsEnabled(LogLevel.Error).ShouldBeFalse();
    }
}
