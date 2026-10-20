using Bunit;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.UI.Pages.Servers.Components;

namespace LANCommander.Server.UI.Tests.Controls;

/// <summary>The server console's line list and command bar (UI/Pages/Servers/Components).</summary>
public class ServerConsoleTests : ControlsTestContext
{
    [Theory]
    [InlineData(ServerLogLevel.Info, "info", "INFO")]
    [InlineData(ServerLogLevel.Ready, "ready", "READY")]
    [InlineData(ServerLogLevel.Warn, "warn", "WARN")]
    [InlineData(ServerLogLevel.Error, "error", "ERROR")]
    [InlineData(ServerLogLevel.Trace, "trace", "TRACE")]
    public void LogRow_ShowsLevelLabelAndClass(ServerLogLevel level, string suffix, string label)
    {
        var row = Render<ServerLogRow>(p => p
            .Add(x => x.Line, new ServerConsoleLine(new DateTime(2026, 9, 28, 21, 4, 12, DateTimeKind.Utc), level, "Map loaded in 6.2s")));

        var div = row.Find(".server-log-row");

        Assert.Contains($"server-log-row-{suffix}", div.ClassList);
        Assert.Equal(label, row.Find(".server-log-level").TextContent);
        Assert.Equal("Map loaded in 6.2s", row.Find(".server-log-message").TextContent);
    }

    [Fact]
    public void LogRow_ShowsTimeInTheGivenZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");

        var row = Render<ServerLogRow>(p => p
            .Add(x => x.Line, new ServerConsoleLine(new DateTime(2026, 9, 28, 19, 4, 12, DateTimeKind.Utc), ServerLogLevel.Info, "x"))
            .Add(x => x.TimeZone, zone));

        Assert.Equal("21:04:12", row.Find(".server-log-time").TextContent);
    }

    [Fact]
    public void LogRow_LeavesTimeBlankForLogFileLines()
    {
        var row = Render<ServerLogRow>(p => p.Add(x => x.Line, ServerConsoleLine.Classified("Server listening on 0.0.0.0:27015")));

        Assert.Equal("", row.Find(".server-log-time").TextContent);
        Assert.Contains("server-log-row-ready", row.Find(".server-log-row").ClassList);
    }

    [Fact]
    public void LogRow_EchoedCommandUsesThePrompt()
    {
        var row = Render<ServerLogRow>(p => p.Add(x => x.Line, ServerConsoleLine.Echo("changelevel ns2_summit")));

        Assert.Contains("server-log-row-command", row.Find(".server-log-row").ClassList);
        Assert.Equal("▶", row.Find(".server-log-level").TextContent);
        Assert.Equal("changelevel ns2_summit", row.Find(".server-log-message").TextContent);
    }

    [Fact]
    public void ConsoleLine_FromServerLogLine_KeepsTimestampAndLevel()
    {
        var at = new DateTime(2026, 9, 28, 21, 12, 20, DateTimeKind.Utc);
        var line = ServerConsoleLine.From(new ServerLogLine(Guid.NewGuid(), null, at, ServerLogLevel.Error, "Lua error in Alien.lua:412"));

        Assert.Equal(at, line.Timestamp);
        Assert.Equal(ServerLogLevel.Error, line.Level);
        Assert.Equal(ServerConsoleLineKind.Output, line.Kind);
    }

    [Fact]
    public void CommandBar_Enabled_SendsTrimmedCommandOnEnterAndClears()
    {
        var sent = new List<string>();

        var bar = Render<ServerCommandBar>(p => p
            .Add(x => x.Enabled, true)
            .Add(x => x.OnSend, (string command) => sent.Add(command)));

        var input = bar.Find("input");

        Assert.False(input.HasAttribute("disabled"));
        Assert.Equal("Send a command", input.GetAttribute("placeholder"));
        Assert.Equal("Enter", bar.Find(".server-command-key").TextContent);

        input.Input("  status  ");
        bar.Find("input").KeyDown("Enter");

        Assert.Equal(["status"], sent);
        Assert.Equal("", bar.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void CommandBar_IgnoresOtherKeysAndBlankCommands()
    {
        var sent = new List<string>();

        var bar = Render<ServerCommandBar>(p => p
            .Add(x => x.Enabled, true)
            .Add(x => x.OnSend, (string command) => sent.Add(command)));

        bar.Find("input").KeyDown("Enter");
        bar.Find("input").Input("status");
        bar.Find("input").KeyDown("a");

        Assert.Empty(sent);
    }

    [Fact]
    public void CommandBar_Disabled_BlocksInputAndExplainsWhy()
    {
        var sent = new List<string>();

        var bar = Render<ServerCommandBar>(p => p
            .Add(x => x.Enabled, false)
            .Add(x => x.DisabledReason, "Start the server to send it commands")
            .Add(x => x.OnSend, (string command) => sent.Add(command)));

        Assert.True(bar.Find("input").HasAttribute("disabled"));
        Assert.Contains("server-command-disabled", bar.Find(".server-command").ClassList);
        Assert.NotNull(bar.Find(".server-command-tooltip"));

        bar.Find("input").KeyDown("Enter");

        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(45, "45s")]
    [InlineData(41 * 60, "41m")]
    [InlineData(2 * 3600 + 41 * 60, "2h 41m")]
    [InlineData(26 * 3600, "1d 2h")]
    public void LiveState_FormatsUptime(int seconds, string expected)
    {
        var now = new DateTime(2026, 9, 28, 21, 0, 0, DateTimeKind.Utc);
        var state = new ServerLiveState(ServerProcessStatus.Running, new ServerProcessInfo(4812, now.AddSeconds(-seconds), 18, 1L << 30));

        Assert.Equal(expected, state.Uptime(now));
    }

    [Fact]
    public void LiveState_NoProcess_HasNoUptime()
    {
        Assert.Null(ServerLiveState.Unknown.Uptime(DateTime.UtcNow));
    }
}
