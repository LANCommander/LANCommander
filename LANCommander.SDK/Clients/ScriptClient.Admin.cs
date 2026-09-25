using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services;

/// <summary>The server's copy of a script changed after the edit being uploaded was started.</summary>
public class ScriptConflictException(Guid scriptId)
    : Exception($"Script {scriptId} was changed on the server after it was opened.")
{
    public Guid ScriptId { get; } = scriptId;
}

public partial class ScriptClient
{
    /// <summary>
    /// Replace a script's contents on the server. Requires an administrator.
    /// </summary>
    /// <param name="scriptId">The server's id for the script.</param>
    /// <param name="contents">The new script text, without the <c>#Requires -RunAsAdministrator</c> header the launcher adds on disk.</param>
    /// <param name="baseContents">
    /// The server contents the edit started from, or null to overwrite unconditionally. When given, the
    /// upload is refused with <see cref="ScriptConflictException"/> if the server copy has changed since.
    /// </param>
    public async Task<Script> UpdateContentsAsync(Guid scriptId, string contents, string baseContents = null)
    {
        var request = new UpdateScriptContentsRequest
        {
            Contents = contents,
            BaseContentsHash = baseContents is null ? null : ScriptHelper.HashContents(baseContents),
        };

        try
        {
            return await serviceProvider
                .GetRequiredService<ApiRequestFactory>()
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Scripts/{scriptId}/Contents")
                .AddBody(request)
                .PostAsync<Script>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ScriptConflictException(scriptId);
        }
    }
}
