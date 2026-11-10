using System.Collections.Concurrent;
using System.Text.Json;
using LANCommander.Interposer.Events;
using LANCommander.Launcher.Services.Interposer;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Interposer;

public class MatchForwarderTests
{
    // What the DLL sends, as LANCommander.Interposer's matches.cpp writes it
    private const string Start = """
        {"externalId":"round-7","map":"de_dust2","mode":"competitive","startedOn":"2026-11-07T20:29:12.345Z",
         "settings":{"timelimit":20},"reporterKind":"Server","reporterName":"PAT-PC","forward":true}
        """;

    private const string Participant = """{"playerKey":"steam:STEAM_0:1:1","displayName":"Pat","team":"CT","joinedAtMilliseconds":0}""";

    private const string Kill = """
        {"id":"0b4a3c3e-8d2f-4c56-9a7e-2f1d3c4b5a69","sequence":1,"type":"kill","gameTimeMilliseconds":4200,"tick":268,
         "actorKey":"steam:STEAM_0:1:1","subjectKey":"bot:3","payload":{"weapon":"ak47"},"clientTime":"2026-11-07T20:30:00.000Z"}
        """;

    private const string End = """
        {"endedOn":"2026-11-07T20:50:00.000Z","reason":"completed","abandoned":false,
         "results":[{"playerKey":"steam:STEAM_0:1:1","rank":1,"score":42.5,"status":"Finished"}],
         "statistics":[{"playerKey":"steam:STEAM_0:1:1","key":"kills","value":3}]}
        """;

    private sealed class FakeRecording : MatchForwarder.IRecording
    {
        public ConcurrentQueue<string> Steps { get; } = new();
        public MatchEndRequest? Ended { get; private set; }
        public bool Disposed { get; private set; }

        public void Record(string type, long gameTimeMilliseconds, string? actorKey, string? subjectKey, JsonElement? payload, long? tick)
            => Steps.Enqueue($"event {type} {gameTimeMilliseconds} {actorKey} {subjectKey} {payload?.GetRawText()} {tick}");

        public Task UpsertParticipantsAsync(IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken)
        {
            foreach (var p in participants)
                Steps.Enqueue($"player {p.PlayerKey} {p.DisplayName} {p.Team}");

            return Task.CompletedTask;
        }

        public Task EndAsync(MatchEndRequest request, CancellationToken cancellationToken)
        {
            Ended = request;
            Steps.Enqueue("end");
            return Task.CompletedTask;
        }

        public Task UploadAttachmentAsync(MatchAttachmentCreateRequest request, Stream content, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(content);
            Steps.Enqueue($"attach {request.Kind} {request.FileName} {reader.ReadToEnd()}");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public Guid GameId { get; } = Guid.NewGuid();
        public List<MatchCreateRequest> Started { get; } = new();
        public FakeRecording Recording { get; } = new();
        public Func<Task>? BeforeStart { get; set; }

        public MatchForwarder.Tracker Track() => new(GameId, null, async (request, _) =>
        {
            if (BeforeStart != null)
                await BeforeStart();

            Started.Add(request);
            return Recording;
        }, NullLogger.Instance);
    }

    [Fact]
    public async Task AForwardedMatchIsRecordedStepByStep()
    {
        var harness = new Harness();
        var tracker = harness.Track();

        tracker.Handle(MatchReportKind.Start, 1, Start);
        tracker.Handle(MatchReportKind.Participant, 1, Participant);
        tracker.Handle(MatchReportKind.Event, 1, Kill);
        tracker.Handle(MatchReportKind.End, 1, End);

        await tracker.DrainAsync();

        var request = harness.Started.ShouldHaveSingleItem();
        request.GameId.ShouldBe(harness.GameId);
        request.ExternalId.ShouldBe("round-7");
        request.Map.ShouldBe("de_dust2");
        request.Settings!.Value.GetProperty("timelimit").GetInt32().ShouldBe(20);
        request.ReporterName.ShouldBe("PAT-PC");

        harness.Recording.Steps.ShouldBe(new[]
        {
            "player steam:STEAM_0:1:1 Pat CT",
            """event kill 4200 steam:STEAM_0:1:1 bot:3 {"weapon":"ak47"} 268""",
            "end",
        });

        var end = harness.Recording.Ended!;
        end.Reason.ShouldBe("completed");
        end.Results.ShouldHaveSingleItem().Score.ShouldBe(42.5);
        end.Results[0].Status.ShouldBe(MatchResultStatus.Finished);
        end.Statistics.ShouldHaveSingleItem().Value.ShouldBe(3);
    }

    [Fact]
    public void AClientReportsAsAClientUnlessItIsHosting()
    {
        var gameId = Guid.NewGuid();

        // Config.yml's default ReporterKind is Server, but a launcher is a player's client
        MatchForwarder.ToCreateRequest(new MatchStart { Forward = true, ReporterKind = "Server" }, gameId)!
            .ReporterKind.ShouldBe(MatchReporterKind.Client);

        MatchForwarder.ToCreateRequest(new MatchStart { Forward = true, ReporterKind = "host" }, gameId)!
            .ReporterKind.ShouldBe(MatchReporterKind.Host);
    }

    [Fact]
    public async Task AMatchTheDllSendsItselfIsLeftAlone()
    {
        var harness = new Harness();
        var tracker = harness.Track();

        tracker.Handle(MatchReportKind.Start, 2, Start.Replace("\"forward\":true", "\"forward\":false"));
        tracker.Handle(MatchReportKind.Event, 2, Kill);
        tracker.Handle(MatchReportKind.End, 2, End);

        await tracker.DrainAsync();

        harness.Started.ShouldBeEmpty();
        harness.Recording.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task StepsWaitForTheMatchToBeRegistered()
    {
        var registered = new TaskCompletionSource();
        var harness = new Harness { BeforeStart = () => registered.Task };
        var tracker = harness.Track();

        tracker.Handle(MatchReportKind.Start, 3, Start);
        tracker.Handle(MatchReportKind.Event, 3, Kill);

        harness.Recording.Steps.ShouldBeEmpty();

        registered.SetResult();
        await tracker.DrainAsync();

        harness.Recording.Steps.Count.ShouldBe(1);
    }

    [Fact]
    public async Task StepsOfAMatchWhoseStartFailedAreDropped()
    {
        var harness = new Harness { BeforeStart = () => throw new HttpRequestException("offline") };
        var tracker = harness.Track();

        tracker.Handle(MatchReportKind.Start, 4, Start);
        tracker.Handle(MatchReportKind.Event, 4, Kill);
        tracker.Handle(MatchReportKind.End, 4, End);

        await tracker.DrainAsync();

        harness.Recording.Steps.ShouldBeEmpty();
    }

    [Fact]
    public async Task AttachmentsAreUploadedFromTheGamesFile()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "DEMO");

        try
        {
            var harness = new Harness();
            var tracker = harness.Track();

            tracker.Handle(MatchReportKind.Start, 5, Start);
            tracker.Handle(MatchReportKind.Attach, 5, JsonSerializer.Serialize(new
            {
                path,
                kind = "Demo",
                fileName = "round.dem",
                contentType = "application/octet-stream",
                size = 4,
            }));

            await tracker.DrainAsync();

            harness.Recording.Steps.ShouldBe(new[] { "attach Demo round.dem DEMO" });
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DisposingFlushesWhatTheMatchRecorded()
    {
        var harness = new Harness();
        var tracker = harness.Track();

        tracker.Handle(MatchReportKind.Start, 6, Start);
        await tracker.DrainAsync();

        tracker.Dispose();
        tracker.Handle(MatchReportKind.Event, 6, Kill);

        // The flush runs after the match's queued steps; give it a moment
        for (var i = 0; i < 50 && !harness.Recording.Disposed; i++)
            await Task.Delay(20);

        harness.Recording.Disposed.ShouldBeTrue();
        harness.Recording.Steps.ShouldBeEmpty();
    }
}
