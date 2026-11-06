using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Services
{
    public class IssueClient(ApiRequestFactory apiRequestFactory)
    {
        public Task<bool> Open(string description, Guid gameId)
            => Open(null, description, gameId);

        public async Task<bool> Open(string title, string description, Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Issue/Open")
                .AddBody(new Issue
                {
                    Title = title,
                    Description = description,
                    GameId = gameId,
                })
                .PostAsync<bool>();
        }

        /// <summary>
        /// Gets how many files the server accepts per issue and how large each may be.
        /// </summary>
        public async Task<IssueAttachmentLimits> GetLimitsAsync()
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Issue/Limits")
                .GetAsync<IssueAttachmentLimits>() ?? IssueAttachmentLimits.Default;
        }

        /// <summary>
        /// Opens an issue with files attached, such as screenshots or logs. The files are checked
        /// against <see cref="IssueAttachmentRules"/> and the server's limits before anything is sent.
        /// </summary>
        /// <param name="limits">The server's limits, if already fetched; otherwise they are fetched first.</param>
        /// <returns>The new issue, including its attachments.</returns>
        /// <exception cref="ArgumentException">A file breaks <see cref="IssueAttachmentRules"/> or the limits.</exception>
        /// <exception cref="HttpRequestException">The server refused the issue; the message says why.</exception>
        public async Task<Issue> OpenAsync(string title, string description, Guid gameId, IEnumerable<string> attachmentPaths, IssueAttachmentLimits limits = null)
        {
            var files = (attachmentPaths ?? []).Select(path => new FileInfo(path)).ToList();

            if (files.Count > 0)
                limits ??= await GetLimitsAsync();

            var problem = IssueAttachmentRules.Validate(files.Select(f => (f.Name, f.Length)).ToList(), limits);

            if (problem != null)
                // No parameter name: it would be appended to the message, which is shown to users
                throw new ArgumentException(problem);

            var streams = new List<Stream>();

            try
            {
                using var form = new MultipartFormDataContent();

                form.Add(new StringContent(gameId.ToString()), "gameId");
                form.Add(new StringContent(title ?? string.Empty), "title");
                form.Add(new StringContent(description ?? string.Empty), "description");

                foreach (var file in files)
                {
                    var stream = file.OpenRead();
                    streams.Add(stream);
                    form.Add(new StreamContent(stream), "attachments", file.Name);
                }

                var result = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute("/api/Issue")
                    .UseMethod(HttpMethod.Post)
                    .AddContent(form)
                    .SendAsync<Issue>();

                if (!result.Response.IsSuccessStatusCode || result.Data == null)
                    throw new HttpRequestException(await ReadErrorAsync(result.Response), null, result.Response.StatusCode);

                return result.Data;
            }
            finally
            {
                foreach (var stream in streams)
                    stream.Dispose();
            }
        }

        /// <summary>
        /// Gets every issue reported against a game, pinned issues first, then unresolved ones,
        /// newest first within each group.
        /// </summary>
        public async Task<IEnumerable<Issue>> GetForGameAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Issue/Game/{gameId}")
                .GetAsync<IEnumerable<Issue>>() ?? [];
        }

        /// <summary>Downloads an attachment to the given file path.</summary>
        public async Task<FileInfo> DownloadAttachmentAsync(Guid attachmentId, string destination)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Issue/Attachments/{attachmentId}/Download")
                .DownloadAsync(destination);
        }

        /// <summary>The server sends refusals as a JSON string; fall back to the status if it didn't.</summary>
        private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            try
            {
                if (JsonSerializer.Deserialize<string>(body) is { Length: > 0 } message)
                    return message;
            }
            catch (JsonException)
            {
            }

            return $"The server could not open the issue ({(int)response.StatusCode} {response.ReasonPhrase}).";
        }
    }
}
