using System.Collections.Concurrent;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.ServerEngines;
using LANCommander.Server.Services.Utilities;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using DataServerConsole = LANCommander.Server.Data.Models.ServerConsole;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// <see cref="LogFileMonitor"/> follows a log file like <c>tail -F</c>: only what is written after it
/// starts, through the file being created late, truncated or rotated.
/// </summary>
public sealed class LogFileMonitorTests : IDisposable
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(20);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lc-logfilemonitor", Guid.NewGuid().ToString("N"));
    private readonly string _path;
    private readonly ConcurrentQueue<string> _lines = new();

    public LogFileMonitorTests()
    {
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "server.log");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private LogFileMonitor Follow() => new(_path, _lines.Enqueue, Poll);

    private void Append(string text) => Append(Encoding.UTF8.GetBytes(text));

    private void Append(byte[] bytes)
    {
        // As a server writes: the file stays shared for reading
        using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);

        stream.Write(bytes);
    }

    private async Task WaitForAsync(params string[] lines)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (!_lines.ToArray().SequenceEqual(lines))
        {
            if (DateTime.UtcNow > deadline)
                _lines.ToArray().ShouldBe(lines);

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task OnlyLinesWrittenAfterItStartsArePassedOn()
    {
        Append("old line\n");

        await using var monitor = Follow();

        Append("first\r\nsecond\n");

        await WaitForAsync("first", "second");
    }

    [Fact]
    public async Task AFileCreatedLaterIsReadFromItsStart()
    {
        await using var monitor = Follow();

        await Task.Delay(Poll * 3);

        Append("created\n");

        await WaitForAsync("created");
    }

    [Fact]
    public async Task APartLineWaitsForTheRestOfIt()
    {
        await using var monitor = Follow();

        Append("Map lo");
        await Task.Delay(Poll * 5);

        _lines.ShouldBeEmpty();

        Append("aded\n");

        await WaitForAsync("Map loaded");
    }

    [Fact]
    public async Task ACharacterSplitAcrossWritesIsDecodedWhole()
    {
        await using var monitor = Follow();

        var bytes = Encoding.UTF8.GetBytes("Spieler Jürgen 🎮\n");
        var split = Array.IndexOf(bytes, (byte)0xF0) + 2;

        Append(bytes[..split]);
        await Task.Delay(Poll * 5);
        Append(bytes[split..]);

        await WaitForAsync("Spieler Jürgen 🎮");
    }

    [Fact]
    public async Task ATruncatedFileIsReadFromItsStart()
    {
        Append("from an earlier run, long enough to be longer than what follows\n");

        await using var monitor = Follow();

        Append("one\n");
        await WaitForAsync("one");

        File.WriteAllText(_path, "");
        Append("two\n");

        await WaitForAsync("one", "two");
    }

    [Fact]
    public async Task ARotatedFileIsFollowedByTheNewOne()
    {
        await using var monitor = Follow();

        Append("before\n");
        await WaitForAsync("before");

        File.Move(_path, _path + ".1");
        Append("after\n");

        await WaitForAsync("before", "after");
    }

    [Fact]
    public async Task WhatIsWrittenBeforeItStopsIsKept()
    {
        var monitor = new LogFileMonitor(_path, _lines.Enqueue, TimeSpan.FromHours(1));

        Append("last words\nno newline");

        await monitor.DisposeAsync();

        _lines.ShouldBe(["last words", "no newline"]);
    }

    [Fact]
    public void AConsolesPathIsUnderTheServersWorkingDirectoryUnlessAbsolute()
    {
        var server = new ServerModel { WorkingDirectory = _directory };

        LogFileMonitor.ResolvePath(server, new DataServerConsole { Path = Path.Combine("logs", "server.log") })
            .ShouldBe(Path.Combine(_directory, "logs", "server.log"));

        LogFileMonitor.ResolvePath(server, new DataServerConsole { Path = _path })
            .ShouldBe(_path);
    }
}

/// <summary>A local server's log file consoles are followed while it runs, and shown as theirs.</summary>
[Collection("Application")]
public class LocalServerLogFileTests(ApplicationFixture fixture) : DalTest(fixture)
{
    [Fact]
    public async Task WhatTheServerWritesToItsLogFileReachesTheConsole()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lc-local-logfile", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            var (path, arguments) = OperatingSystem.IsWindows()
                ? ("cmd.exe", "/c \"echo started>>server.log & echo map loaded>>server.log\"")
                : ("/bin/sh", "-c \"echo started >> server.log; echo map loaded >> server.log\"");

            var server = await GetService<ServerService>().AddAsync(new ServerModel
            {
                Name = Unique("Logging"),
                Engine = ServerEngine.Local,
                Path = path,
                Arguments = arguments,
                WorkingDirectory = directory,
            });

            var console = await GetService<ServerConsoleService>().AddAsync(new DataServerConsole
            {
                Name = "Server log",
                Type = ServerConsoleType.LogFile,
                Path = "server.log",
                ServerId = server.Id,
            });

            var engine = ApplicationFixture.Instance.ServiceProvider.GetServices<IServerEngine>().OfType<LocalServerEngine>().Single();
            var lines = new ConcurrentQueue<ServerLogEventArgs>();

            void OnLog(object? sender, ServerLogEventArgs args)
            {
                if (args.ServerId == server.Id && args.Log != null)
                    lines.Enqueue(args);
            }

            engine.OnServerLog += OnLog;

            try
            {
                // Returns once the process has exited
                await engine.StartAsync(server.Id).WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally
            {
                engine.OnServerLog -= OnLog;
            }

            lines.Select(l => l.Line.Trim()).ShouldBe(["started", "map loaded"]);
            lines.ShouldAllBe(l => l.Log!.Id == console.Id);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
