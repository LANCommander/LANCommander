using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services
{
    /// <summary>
    /// Records one match's events from one reporter. Each event gets its id and sequence number when it
    /// is recorded, so resending after a failure never duplicates it. Events are sent in batches every
    /// <see cref="FlushInterval"/> and when the match ends; ones the server couldn't take yet stay
    /// queued and go with the next flush.
    /// </summary>
    /// <remarks>
    /// Events are held in memory. A reporter that must survive a crash should keep its own copy and,
    /// after restarting, resume with <see cref="MatchClient.StartRecordingAsync"/> using the same
    /// external id, then resend everything after <see cref="MatchRegistration.LastSequence"/>.
    /// </remarks>
    public sealed class MatchRecorder : IAsyncDisposable
    {
        private readonly MatchClient _client;
        private readonly List<MatchEventSubmission> _pending = new();
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private readonly Timer _timer;
        private long _sequence;

        internal MatchRecorder(MatchClient client, MatchRegistration registration)
        {
            _client = client;
            Registration = registration;
            _sequence = registration.LastSequence;
            _timer = new Timer(_ => _ = FlushQuietlyAsync(), null, FlushInterval, FlushInterval);
        }

        public MatchRegistration Registration { get; }

        public Guid MatchId => Registration.MatchId;

        public static TimeSpan FlushInterval { get; } = TimeSpan.FromSeconds(2);

        /// <summary>Events rejected by the server, which won't be retried. Useful for spotting a bad reporter.</summary>
        public List<MatchEventRejection> Rejected { get; } = new();

        /// <summary>Events recorded and not yet taken by the server.</summary>
        public int PendingCount
        {
            get { lock (_pending) return _pending.Count; }
        }

        /// <summary>
        /// Records an event. <paramref name="payload"/> is any object serialized as JSON, such as an
        /// anonymous object: <c>new { weapon = "rifle", headshot = true }</c>.
        /// </summary>
        public MatchEventSubmission Record(string type, long gameTimeMilliseconds, string actorKey = null, string subjectKey = null, object payload = null, long? tick = null)
        {
            var submission = new MatchEventSubmission
            {
                Id = NewEventId(),
                Sequence = Interlocked.Increment(ref _sequence),
                Type = type,
                GameTimeMilliseconds = gameTimeMilliseconds,
                Tick = tick,
                ActorKey = actorKey,
                SubjectKey = subjectKey,
                Payload = payload == null ? null : payload is JsonElement element ? element : JsonSerializer.SerializeToElement(payload),
                ClientTime = DateTime.UtcNow,
            };

            lock (_pending)
                _pending.Add(submission);

            return submission;
        }

        /// <summary>Adds or updates players in the match straight away.</summary>
        public Task UpsertParticipantsAsync(IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken = default) =>
            _client.UpsertParticipantsAsync(MatchId, participants, cancellationToken);

        /// <summary>Sends everything recorded so far, waiting out the server when it's busy.</summary>
        public async Task FlushAsync(CancellationToken cancellationToken = default)
        {
            await _flushLock.WaitAsync(cancellationToken);

            try
            {
                while (true)
                {
                    List<MatchEventSubmission> batch;

                    lock (_pending)
                        batch = _pending.Take(MatchClient.DefaultBatchSize).ToList();

                    if (batch.Count == 0)
                        return;

                    MatchEventBatchResult result;

                    try
                    {
                        result = await _client.SendEventsAsync(MatchId, new MatchEventBatch { ReporterId = Registration.ReporterId, Events = batch }, cancellationToken);
                    }
                    catch (MatchServerBusyException busy)
                    {
                        await Task.Delay(busy.RetryAfter, cancellationToken);
                        continue;
                    }

                    lock (_pending)
                        _pending.RemoveRange(0, batch.Count);

                    if (result?.Rejected != null)
                        Rejected.AddRange(result.Rejected);
                }
            }
            finally
            {
                _flushLock.Release();
            }
        }

        /// <summary>Sends what's left and ends the match with its standings and totals.</summary>
        public async Task EndAsync(MatchEndRequest request, CancellationToken cancellationToken = default)
        {
            await FlushAsync(cancellationToken);
            await _client.EndAsync(MatchId, request, cancellationToken);
        }

        /// <summary>Attaches a file, such as a demo, to the match as this reporter.</summary>
        public Task<MatchAttachmentInformation> UploadAttachmentAsync(MatchAttachmentCreateRequest request, System.IO.Stream content, CancellationToken cancellationToken = default)
        {
            request.ReporterId ??= Registration.ReporterId;

            return _client.UploadAttachmentAsync(MatchId, request, content, cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _timer.DisposeAsync();

            try
            {
                await FlushAsync();
            }
            catch
            {
                // Best effort: the caller chose not to end the match explicitly
            }

            _flushLock.Dispose();
        }

        private async Task FlushQuietlyAsync()
        {
            // A timer flush that finds one already running skips; the running one sends everything
            if (_flushLock.CurrentCount == 0)
                return;

            try
            {
                await FlushAsync();
            }
            catch
            {
                // Network trouble: the events stay pending for the next flush
            }
        }

        private static Guid NewEventId()
        {
#if NET9_0_OR_GREATER
            // Time-ordered, so ids sort roughly in the order events happened
            return Guid.CreateVersion7();
#else
            return Guid.NewGuid();
#endif
        }
    }
}
