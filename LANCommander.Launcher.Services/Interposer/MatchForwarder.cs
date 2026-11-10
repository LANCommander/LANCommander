using System.Text.Json;
using LANCommander.Interposer;
using LANCommander.Interposer.Events;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Interposer;

/// <summary>
/// Reports the matches a game running under the Interposer records through its match API to the
/// server, as the signed-in player. A patch calls the API; the DLL sends each step over the event
/// pipe; this turns them into a <see cref="MatchRecorder"/>'s calls, which number, batch and retry.
/// </summary>
/// <remarks>
/// Only matches whose start says <see cref="MatchStart.Forward"/> are reported. The others have a
/// LANCommander server of their own in the game's Config.yml, and the DLL sends them itself; reporting
/// them here as well would count them twice. Reports from a player's launcher are kept as that
/// player's own view of the match; the server decides the trust, not the launcher.
/// </remarks>
public sealed class MatchForwarder(MatchClient matchClient, ILogger<MatchForwarder> logger)
{
    /// <summary>What the forwarder needs of a match being recorded: the slice of <see cref="MatchRecorder"/> it calls.</summary>
    internal interface IRecording : IAsyncDisposable
    {
        void Record(string type, long gameTimeMilliseconds, string? actorKey, string? subjectKey, JsonElement? payload, long? tick);
        Task UpsertParticipantsAsync(IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken);
        Task EndAsync(MatchEndRequest request, CancellationToken cancellationToken);
        Task UploadAttachmentAsync(MatchAttachmentCreateRequest request, Stream content, CancellationToken cancellationToken);
    }

    /// <summary>Forwards the game's matches until the result is disposed, when the game has exited.</summary>
    public IDisposable Track(Guid gameId, InterposerService interposer)
        => new Tracker(gameId, interposer, async (request, token) => new Recording(await matchClient.StartRecordingAsync(request, token)), logger);

    // The DLL's JSON is camelCase, enums by name: what the SDK's models read with the web defaults.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The request a match's start becomes, or null when the launcher is not the one to report it.
    /// A launcher is a player's client, so it reports as one unless the game says it is hosting.
    /// </summary>
    internal static MatchCreateRequest? ToCreateRequest(MatchStart? start, Guid gameId)
    {
        if (start is not { Forward: true })
            return null;

        return new MatchCreateRequest
        {
            GameId = Guid.TryParse(start.GameId, out var configured) ? configured : gameId,
            ServerId = Guid.TryParse(start.ServerId, out var serverId) ? serverId : null,
            ExternalId = start.ExternalId,
            Map = start.Map,
            Mode = start.Mode,
            GameVersion = start.GameVersion,
            ServerName = start.ServerName,
            ServerAddress = start.ServerAddress,
            StartedOn = start.StartedOn == default ? null : start.StartedOn.ToUniversalTime(),
            Settings = start.Settings,
            Metadata = start.Metadata,
            ReporterKind = string.Equals(start.ReporterKind, "Host", StringComparison.OrdinalIgnoreCase)
                ? MatchReporterKind.Host
                : MatchReporterKind.Client,
            ReporterName = start.ReporterName,
        };
    }

    internal static MatchAttachmentKind ToAttachmentKind(string? kind)
        => Enum.TryParse<MatchAttachmentKind>(kind, ignoreCase: true, out var parsed) ? parsed : MatchAttachmentKind.Other;

    /// <summary>A <see cref="MatchRecorder"/> behind <see cref="IRecording"/>.</summary>
    private sealed class Recording(MatchRecorder recorder) : IRecording
    {
        public void Record(string type, long gameTimeMilliseconds, string? actorKey, string? subjectKey, JsonElement? payload, long? tick)
            => recorder.Record(type, gameTimeMilliseconds, actorKey, subjectKey, payload, tick);

        public Task UpsertParticipantsAsync(IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken)
            => recorder.UpsertParticipantsAsync(participants, cancellationToken);

        public Task EndAsync(MatchEndRequest request, CancellationToken cancellationToken)
            => recorder.EndAsync(request, cancellationToken);

        public Task UploadAttachmentAsync(MatchAttachmentCreateRequest request, Stream content, CancellationToken cancellationToken)
            => recorder.UploadAttachmentAsync(request, content, cancellationToken);

        public ValueTask DisposeAsync() => recorder.DisposeAsync();
    }

    /// <summary>One game's matches. Each match's steps run one after another, in the order the game took them.</summary>
    internal sealed class Tracker : IDisposable
    {
        private readonly Guid _gameId;
        private readonly InterposerService? _interposer;
        private readonly Func<MatchCreateRequest, CancellationToken, Task<IRecording>> _start;
        private readonly ILogger _logger;
        private readonly object _lock = new();
        private readonly Dictionary<uint, MatchState> _matches = new();
        private readonly HashSet<uint> _ignored = new();
        private bool _disposed;

        private sealed class MatchState
        {
            public Task Tail = Task.CompletedTask;
            public IRecording? Recording;
            public string ExternalId = string.Empty;
        }

        internal Tracker(
            Guid gameId,
            InterposerService? interposer,
            Func<MatchCreateRequest, CancellationToken, Task<IRecording>> start,
            ILogger logger)
        {
            _gameId = gameId;
            _interposer = interposer;
            _start = start;
            _logger = logger;

            if (_interposer != null)
                _interposer.MatchReported += OnMatchReported;
        }

        private void OnMatchReported(object? sender, MatchReportedEventArgs e) => Handle(e.Kind, e.Match, e.Json);

        /// <summary>Queues one step of a match, as the DLL reported it.</summary>
        internal void Handle(MatchReportKind kind, uint match, string json)
        {
            lock (_lock)
            {
                if (_disposed || _ignored.Contains(match))
                    return;

                if (kind == MatchReportKind.Start)
                {
                    var request = ToCreateRequest(Read<MatchStart>(json), _gameId);

                    if (request == null)
                    {
                        // Sent by the DLL itself, or unreadable
                        _ignored.Add(match);
                        return;
                    }

                    var state = new MatchState { ExternalId = request.ExternalId };
                    _matches[match] = state;

                    Enqueue(state, needsRecording: false, async token =>
                    {
                        state.Recording = await _start(request, token);
                        _logger.LogInformation("Recording match {ExternalId} of game {GameId}", request.ExternalId, _gameId);
                    });

                    return;
                }

                // A step of a match started before the launcher was listening
                if (!_matches.TryGetValue(match, out var existing))
                    return;

                switch (kind)
                {
                    case MatchReportKind.Participant when Read<MatchParticipantSubmission>(json) is { } participant:
                        Enqueue(existing, needsRecording: true, token => existing.Recording!.UpsertParticipantsAsync([participant], token));
                        break;

                    case MatchReportKind.Event when Read<MatchEvent>(json) is { } e:
                        Enqueue(existing, needsRecording: true, _ =>
                        {
                            existing.Recording!.Record(e.Type, e.GameTimeMilliseconds, e.ActorKey, e.SubjectKey, e.Payload, e.Tick);
                            return Task.CompletedTask;
                        });
                        break;

                    case MatchReportKind.End when Read<MatchEndRequest>(json) is { } end:
                        Enqueue(existing, needsRecording: true, token => existing.Recording!.EndAsync(end, token));
                        break;

                    case MatchReportKind.Attach when Read<MatchAttachment>(json) is { } attachment:
                        Enqueue(existing, needsRecording: true, async token =>
                        {
                            await using var content = File.OpenRead(attachment.Path);

                            await existing.Recording!.UploadAttachmentAsync(new MatchAttachmentCreateRequest
                            {
                                Kind = ToAttachmentKind(attachment.Kind),
                                FileName = attachment.FileName,
                                ContentType = attachment.ContentType,
                                StartMilliseconds = attachment.StartMilliseconds,
                                EndMilliseconds = attachment.EndMilliseconds,
                            }, content, token);
                        });
                        break;
                }
            }
        }

        // Caller holds _lock. Every step but the start needs the recording the start made, so the
        // steps of a match whose start failed are dropped with it.
        private void Enqueue(MatchState state, bool needsRecording, Func<CancellationToken, Task> step)
        {
            state.Tail = state.Tail.ContinueWith(async _ =>
            {
                if (needsRecording && state.Recording == null)
                    return;

                try
                {
                    await step(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Could not report a step of match {ExternalId} for game {GameId}", state.ExternalId, _gameId);
                }
            }, TaskScheduler.Default).Unwrap();
        }

        private static T? Read<T>(string json) where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, Json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>Waits for every match's queued steps. For tests.</summary>
        internal Task DrainAsync()
        {
            lock (_lock)
                return Task.WhenAll(_matches.Values.Select(m => m.Tail));
        }

        /// <summary>
        /// Stops listening. What a match recorded and has not sent yet still goes: each recorder is
        /// flushed once its queued steps are done.
        /// </summary>
        public void Dispose()
        {
            List<MatchState> matches;

            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                matches = [.. _matches.Values];
            }

            if (_interposer != null)
                _interposer.MatchReported -= OnMatchReported;

            foreach (var state in matches)
            {
                _ = state.Tail.ContinueWith(async _ =>
                {
                    if (state.Recording == null)
                        return;

                    try
                    {
                        await state.Recording.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Could not flush match {ExternalId} for game {GameId}", state.ExternalId, _gameId);
                    }
                }, TaskScheduler.Default).Unwrap();
            }
        }
    }
}
