using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class ServerLogClassifierTests
{
    [Theory]
    [InlineData("Loading map de_dust2", ServerLogLevel.Info)]
    [InlineData("", ServerLogLevel.Info)]
    [InlineData(null, ServerLogLevel.Info)]
    [InlineData("ERROR: could not bind socket", ServerLogLevel.Error)]
    [InlineData("Fatal exception in thread main", ServerLogLevel.Error)]
    [InlineData("Connection failed", ServerLogLevel.Error)]
    [InlineData("The server crashed", ServerLogLevel.Error)]
    [InlineData("WARNING: low memory", ServerLogLevel.Warn)]
    [InlineData("[warn] tick took 120ms", ServerLogLevel.Warn)]
    [InlineData("Server started", ServerLogLevel.Ready)]
    [InlineData("Listening on 0.0.0.0:27015", ServerLogLevel.Ready)]
    [InlineData("Ready for connections", ServerLogLevel.Ready)]
    [InlineData("[DEBUG] tick 4411", ServerLogLevel.Trace)]
    [InlineData("trace: packet received", ServerLogLevel.Trace)]
    // Precedence: errors beat warnings, warnings beat readiness, debug chatter beats readiness
    [InlineData("warning: listener failed", ServerLogLevel.Error)]
    [InlineData("warning: started without a config", ServerLogLevel.Warn)]
    [InlineData("debug: listening socket opened", ServerLogLevel.Trace)]
    // Whole words only
    [InlineData("Terrorists win", ServerLogLevel.Info)]
    [InlineData("Restarted round", ServerLogLevel.Info)]
    public void ClassifiesLines(string? line, ServerLogLevel expected)
    {
        ServerLogClassifier.Classify(line).ShouldBe(expected);
    }

    [Fact]
    public void LogEventArgsAreTimestampedAndClassifiedOnReceipt()
    {
        var serverId = Guid.NewGuid();
        var before = DateTime.UtcNow;

        var args = new ServerLogEventArgs(serverId, "Server started");

        args.ServerId.ShouldBe(serverId);
        args.Level.ShouldBe(ServerLogLevel.Ready);
        args.Timestamp.ShouldBeInRange(before, DateTime.UtcNow);
        args.Timestamp.Kind.ShouldBe(DateTimeKind.Utc);

        var line = args.ToLogLine();

        line.ShouldBe(new ServerLogLine(serverId, null, args.Timestamp, ServerLogLevel.Ready, "Server started"));
    }

    [Theory]
    [InlineData(1000, 1000, 1, 100d)]
    [InlineData(1000, 1000, 4, 25d)]
    [InlineData(500, 2000, 2, 12.5d)]
    [InlineData(0, 2000, 8, 0d)]
    [InlineData(9000, 1000, 4, 100d)] // clamped
    public void ComputesCpuPercentOfTheWholeMachine(int processorMs, int elapsedMs, int processors, double expected)
    {
        ProcessCpuSampler
            .ComputeCpuPercent(TimeSpan.FromMilliseconds(processorMs), TimeSpan.FromMilliseconds(elapsedMs), processors)
            .ShouldBe(expected);
    }

    [Fact]
    public void CpuPercentIsNullForAnEmptyWindow()
    {
        ProcessCpuSampler.ComputeCpuPercent(TimeSpan.FromSeconds(1), TimeSpan.Zero, 4).ShouldBeNull();
        ProcessCpuSampler.ComputeCpuPercent(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1), 4).ShouldBeNull();
    }
}
