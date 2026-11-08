using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services
{
    /// <summary>
    /// Reports multiplayer matches to the LANCommander server and reads them back. For reporting, start
    /// with <see cref="StartRecordingAsync"/>, which numbers, batches and resends events for you.
    /// </summary>
    public class MatchClient(ApiRequestFactory apiRequestFactory)
    {
        /// <summary>The most events the server takes in one request by default.</summary>
        public const int DefaultBatchSize = 500;

        /// <summary>Starts or resumes a match and returns a recorder for its events.</summary>
        public async Task<MatchRecorder> StartRecordingAsync(MatchCreateRequest request, CancellationToken cancellationToken = default)
        {
            var registration = await CreateAsync(request, cancellationToken);

            return new MatchRecorder(this, registration);
        }

        /// <summary>Starts a match, or returns the one already recorded under the same external id.</summary>
        public async Task<MatchRegistration> CreateAsync(MatchCreateRequest request, CancellationToken cancellationToken = default) =>
            await Request("/api/Matches", cancellationToken)
                .AddBody(request)
                .PostAsync<MatchRegistration>();

        public async Task UpsertParticipantsAsync(Guid matchId, IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken = default) =>
            await Request($"/api/Matches/{matchId}/Participants", cancellationToken)
                .AddBody(participants.ToList())
                .PostAsync();

        /// <summary>Sends one batch of events. Prefer <see cref="MatchRecorder"/>, which handles retries.</summary>
        /// <exception cref="MatchServerBusyException">The server is behind; retry the same events later.</exception>
        public async Task<MatchEventBatchResult> SendEventsAsync(Guid matchId, MatchEventBatch batch, CancellationToken cancellationToken = default)
        {
            var response = await Request($"/api/Matches/{matchId}/Events", cancellationToken)
                .UseMethod(HttpMethod.Post)
                .AddBody(batch)
                .SendAsync<MatchEventBatchResult>();

            if (response.Response.StatusCode == (HttpStatusCode)429)
                throw new MatchServerBusyException(response.Response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2));

            response.Response.EnsureSuccessStatusCode();

            return response.Data;
        }

        /// <summary>Ends a match with its standings and totals.</summary>
        public async Task EndAsync(Guid matchId, MatchEndRequest request, CancellationToken cancellationToken = default) =>
            await Request($"/api/Matches/{matchId}/End", cancellationToken)
                .AddBody(request)
                .PostAsync();

        /// <summary>The server's record of a reporter, including the last sequence number it wrote.</summary>
        public async Task<MatchReporterInformation> GetReporterAsync(Guid matchId, Guid reporterId, CancellationToken cancellationToken = default) =>
            await Request($"/api/Matches/{matchId}/Reporters/{reporterId}", cancellationToken)
                .GetAsync<MatchReporterInformation>();

        public async Task<MatchPage<MatchSummary>> GetMatchesAsync(MatchQuery query, CancellationToken cancellationToken = default) =>
            await Request("/api/Matches" + QueryString(
                    ("GameId", query.GameId),
                    ("ServerId", query.ServerId),
                    ("UserId", query.UserId),
                    ("Status", query.Status),
                    ("Map", query.Map),
                    ("Mode", query.Mode),
                    ("From", query.From),
                    ("To", query.To),
                    ("Page", query.Page),
                    ("PageSize", query.PageSize)), cancellationToken)
                .GetAsync<MatchPage<MatchSummary>>();

        public async Task<MatchDetail> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
            await Request($"/api/Matches/{matchId}", cancellationToken)
                .GetAsync<MatchDetail>();

        public async Task<MatchPage<MatchEventInformation>> GetEventsAsync(Guid matchId, MatchEventQuery query, CancellationToken cancellationToken = default) =>
            await Request($"/api/Matches/{matchId}/Events" + QueryString(
                    ("Type", query.Type),
                    ("ActorKey", query.ActorKey),
                    ("SubjectKey", query.SubjectKey),
                    ("AfterGameTimeMilliseconds", query.AfterGameTimeMilliseconds),
                    ("Page", query.Page),
                    ("PageSize", query.PageSize)), cancellationToken)
                .GetAsync<MatchPage<MatchEventInformation>>();

        /// <summary>One statistic aggregated across a game's matches: a leaderboard.</summary>
        public async Task<List<MatchStatisticAggregate>> AggregateAsync(MatchStatisticQuery query, CancellationToken cancellationToken = default) =>
            await Request("/api/Matches/Statistics" + QueryString(
                    ("GameId", query.GameId),
                    ("Key", query.Key),
                    ("Aggregation", query.Aggregation),
                    ("GroupBy", query.GroupBy),
                    ("ServerId", query.ServerId),
                    ("Map", query.Map),
                    ("Mode", query.Mode),
                    ("From", query.From),
                    ("To", query.To),
                    ("ExcludeAbandoned", query.ExcludeAbandoned),
                    ("Descending", query.Descending),
                    ("Limit", query.Limit)), cancellationToken)
                .GetAsync<List<MatchStatisticAggregate>>();

        /// <summary>
        /// Attaches a file, such as a demo, to a match, sending it in chunks. The size and SHA-256 are
        /// worked out from the stream when not given; the stream must be seekable for that.
        /// </summary>
        public async Task<MatchAttachmentInformation> UploadAttachmentAsync(Guid matchId, MatchAttachmentCreateRequest request, Stream content, long chunkSize = 16 * 1024 * 1024, CancellationToken cancellationToken = default)
        {
            if (content.CanSeek)
            {
                var start = content.Position;

                if (request.Size <= 0)
                    request.Size = content.Length - start;

                if (string.IsNullOrEmpty(request.Sha256))
                {
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        request.Sha256 = BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();

                    content.Position = start;
                }
            }

            var attachment = await Request($"/api/Matches/{matchId}/Attachments", cancellationToken)
                .AddBody(request)
                .PostAsync<MatchAttachmentInformation>();

            var buffer = new byte[(int)Math.Min(chunkSize, int.MaxValue)];
            long offset = 0;

            while (offset < request.Size)
            {
                var length = 0;
                int read;

                while (length < buffer.Length && (read = await content.ReadAsync(buffer, length, buffer.Length - length, cancellationToken)) > 0)
                    length += read;

                if (length == 0)
                    break;

                var chunk = new ByteArrayContent(buffer, 0, length);
                chunk.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                await Request($"/api/Matches/{matchId}/Attachments/{attachment.Id}/Content?offset={offset}", cancellationToken)
                    .AddContent(chunk)
                    .PutAsync<object>();

                offset += length;
            }

            var completed = await Request($"/api/Matches/{matchId}/Attachments/{attachment.Id}/Complete", cancellationToken)
                .UseMethod(HttpMethod.Post)
                .SendAsync<MatchAttachmentInformation>();

            return completed.Data;
        }

        public async Task<List<PlayerIdentityInformation>> GetPlayerIdentitiesAsync(Guid? userId = null, CancellationToken cancellationToken = default) =>
            await Request("/api/Matches/Identities" + QueryString(("UserId", userId)), cancellationToken)
                .GetAsync<List<PlayerIdentityInformation>>();

        /// <summary>Links an in-game id, such as <c>steam:STEAM_0:1:123</c>, to a user: yourself unless you're an administrator.</summary>
        public async Task<PlayerIdentityInformation> AddPlayerIdentityAsync(PlayerIdentitySubmission identity, CancellationToken cancellationToken = default) =>
            await Request("/api/Matches/Identities", cancellationToken)
                .AddBody(identity)
                .PostAsync<PlayerIdentityInformation>();

        private Helpers.ApiRequestBuilder Request(string route, CancellationToken cancellationToken) =>
            apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseCancellationToken(cancellationToken)
                .UseRoute(route);

        private static string QueryString(params (string Name, object Value)[] parameters)
        {
            var builder = new StringBuilder();

            foreach (var (name, value) in parameters)
            {
                if (value == null)
                    continue;

                var text = value switch
                {
                    DateTime date => date.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture),
                    bool flag => flag ? "true" : "false",
                    IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                    _ => value.ToString(),
                };

                builder.Append(builder.Length == 0 ? '?' : '&')
                    .Append(Uri.EscapeDataString(name))
                    .Append('=')
                    .Append(Uri.EscapeDataString(text));
            }

            return builder.ToString();
        }
    }

    /// <summary>The server is behind on writing match events. Send the same events again after <see cref="RetryAfter"/>.</summary>
    public class MatchServerBusyException(TimeSpan retryAfter) : Exception("The server is busy writing match events")
    {
        public TimeSpan RetryAfter { get; } = retryAfter;
    }
}
