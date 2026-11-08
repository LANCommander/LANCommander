using System.Security.Claims;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.MatchStatistics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// Multiplayer match statistics. Reporters (game servers, tools, launchers) create a match, send its
/// events in batches and end it with results; anyone signed in can read matches, so dashboards and
/// plugins can build on them. Reports from administrators are trusted; everyone else's are kept as a
/// player's own client's view.
/// </summary>
public static class MatchEndpoints
{
    /// <summary>The largest events request, before the per-event checks.</summary>
    public const long MaxEventsRequestSize = 2 * 1024 * 1024;

    public static void MapMatchEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Matches").RequireAuthorization();

        group.MapGet("/", GetMatchesAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/Statistics", AggregateAsync);

        group.MapGet("/Identities", GetIdentitiesAsync);
        group.MapPost("/Identities", AddIdentityAsync);
        group.MapDelete("/Identities/{identityId:guid}", DeleteIdentityAsync);

        group.MapGet("/{matchId:guid}", GetMatchAsync);
        group.MapPost("/{matchId:guid}/Participants", UpsertParticipantsAsync);
        group.MapPost("/{matchId:guid}/Events", RecordAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaxEventsRequestSize));
        group.MapGet("/{matchId:guid}/Events", GetEventsAsync);
        group.MapPost("/{matchId:guid}/End", EndAsync);
        group.MapGet("/{matchId:guid}/Reporters/{reporterId:guid}", GetReporterAsync);

        group.MapPost("/{matchId:guid}/Attachments", CreateAttachmentAsync);
        group.MapPut("/{matchId:guid}/Attachments/{attachmentId:guid}/Content", WriteAttachmentChunkAsync)
            .WithMetadata(new RequestSizeLimitAttribute(256L * 1024 * 1024));
        group.MapPost("/{matchId:guid}/Attachments/{attachmentId:guid}/Complete", CompleteAttachmentAsync);
        group.MapGet("/{matchId:guid}/Attachments/{attachmentId:guid}/Download", DownloadAttachmentAsync);
        group.MapDelete("/{matchId:guid}/Attachments/{attachmentId:guid}", DeleteAttachmentAsync)
            .RequireAuthorization(RoleService.AdministratorRoleName);
    }

    internal static Task<IResult> CreateAsync(
        MatchCreateRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchService matchService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var caller = await GetCallerAsync(principal, userService);
            var registration = await matchService.CreateAsync(request, caller, cancellationToken);

            return registration.Created
                ? TypedResults.Created($"/api/Matches/{registration.MatchId}", registration)
                : TypedResults.Ok(registration);
        });

    internal static Task<IResult> UpsertParticipantsAsync(
        Guid matchId,
        List<MatchParticipantSubmission> participants,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchService matchService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            await matchService.UpsertParticipantsAsync(matchId, participants, await GetCallerAsync(principal, userService), cancellationToken);

            return TypedResults.NoContent();
        });

    /// <summary>
    /// Queues a batch of events. Answers 202 with the events that will never be accepted, or 429 when
    /// the server is behind; retry those with the same ids.
    /// </summary>
    internal static Task<IResult> RecordAsync(
        Guid matchId,
        MatchEventBatch batch,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchService matchService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var caller = await GetCallerAsync(principal, userService);
            var result = await matchService.RecordAsync(matchId, batch.ReporterId, batch.Events, caller, waitForRoom: false, cancellationToken);

            return TypedResults.Accepted((string?)null, result);
        });

    internal static Task<IResult> EndAsync(
        Guid matchId,
        MatchEndRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchService matchService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            await matchService.EndAsync(matchId, request, await GetCallerAsync(principal, userService), cancellationToken);

            return TypedResults.NoContent();
        });

    internal static Task<IResult> GetReporterAsync(
        Guid matchId,
        Guid reporterId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchService matchService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
            TypedResults.Ok(await matchService.GetReporterAsync(matchId, reporterId, await GetCallerAsync(principal, userService), cancellationToken)));

    internal static async Task<Ok<MatchPage<MatchSummary>>> GetMatchesAsync(
        [AsParameters] MatchQuery query,
        [FromServices] MatchQueryService queryService,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await queryService.GetMatchesAsync(query, cancellationToken));

    internal static async Task<Results<Ok<MatchDetail>, NotFound>> GetMatchAsync(
        Guid matchId,
        [FromServices] MatchQueryService queryService,
        CancellationToken cancellationToken)
    {
        var match = await queryService.GetMatchAsync(matchId, cancellationToken);

        return match == null ? TypedResults.NotFound() : TypedResults.Ok(match);
    }

    internal static Task<IResult> GetEventsAsync(
        Guid matchId,
        [AsParameters] MatchEventQuery query,
        HttpContext httpContext,
        [FromServices] MatchQueryService queryService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () => TypedResults.Ok(await queryService.GetEventsAsync(matchId, query, cancellationToken)));

    internal static Task<IResult> AggregateAsync(
        [AsParameters] MatchStatisticQuery query,
        HttpContext httpContext,
        [FromServices] MatchQueryService queryService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () => TypedResults.Ok(await queryService.AggregateAsync(query, cancellationToken)));

    internal static Task<IResult> CreateAttachmentAsync(
        Guid matchId,
        MatchAttachmentCreateRequest request,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchAttachmentService attachmentService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var attachment = await attachmentService.CreateAsync(matchId, request, await GetCallerAsync(principal, userService), cancellationToken);

            return TypedResults.Created($"/api/Matches/{matchId}/Attachments/{attachment.Id}", attachment);
        });

    /// <summary>Writes the request body into the attachment's file at <paramref name="offset"/>.</summary>
    internal static Task<IResult> WriteAttachmentChunkAsync(
        Guid matchId,
        Guid attachmentId,
        [FromQuery] long offset,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchAttachmentService attachmentService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            await attachmentService.WriteChunkAsync(attachmentId, offset, httpContext.Request.Body, await GetCallerAsync(principal, userService), cancellationToken);

            return TypedResults.NoContent();
        });

    internal static Task<IResult> CompleteAttachmentAsync(
        Guid matchId,
        Guid attachmentId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] MatchAttachmentService attachmentService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var attachment = await attachmentService.CompleteAsync(attachmentId, await GetCallerAsync(principal, userService), cancellationToken);

            return attachment.Status == MatchAttachmentStatus.Uploaded
                ? TypedResults.Ok(attachment)
                : TypedResults.UnprocessableEntity(attachment);
        });

    internal static async Task<IResult> DownloadAttachmentAsync(
        Guid matchId,
        Guid attachmentId,
        [FromServices] MatchAttachmentService attachmentService,
        CancellationToken cancellationToken)
    {
        var opened = await attachmentService.OpenAsync(attachmentId, cancellationToken);

        if (opened is not { } file || file.Attachment.MatchId != matchId)
        {
            if (opened is { } wrong)
                await wrong.Content.DisposeAsync();

            return TypedResults.NotFound();
        }

        return TypedResults.Stream(file.Content, file.Attachment.ContentType, file.Attachment.FileName, enableRangeProcessing: true);
    }

    internal static async Task<IResult> DeleteAttachmentAsync(
        Guid matchId,
        Guid attachmentId,
        [FromServices] MatchAttachmentService attachmentService,
        CancellationToken cancellationToken) =>
        await attachmentService.DeleteAsync(attachmentId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    /// <summary>A user's in-game ids: the caller's own, or anyone's for an administrator.</summary>
    internal static Task<IResult> GetIdentitiesAsync(
        [FromQuery] Guid? userId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] PlayerIdentityService identityService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var caller = await GetCallerAsync(principal, userService);
            var target = userId ?? caller.UserId!.Value;

            if (target != caller.UserId && !caller.IsAdministrator)
                return Forbidden();

            return TypedResults.Ok(await identityService.GetForUserAsync(target, cancellationToken));
        });

    /// <summary>Links an in-game id, such as a Steam ID, to the caller, or to anyone for an administrator.</summary>
    internal static Task<IResult> AddIdentityAsync(
        PlayerIdentitySubmission submission,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] PlayerIdentityService identityService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var caller = await GetCallerAsync(principal, userService);

            if (submission.UserId == Guid.Empty)
                submission.UserId = caller.UserId!.Value;

            if (submission.UserId != caller.UserId && !caller.IsAdministrator)
                return Forbidden();

            var identity = await identityService.AddAsync(submission, PlayerIdentitySource.Manual, caller.UserId, cancellationToken);

            return TypedResults.Ok(identity);
        });

    internal static Task<IResult> DeleteIdentityAsync(
        Guid identityId,
        ClaimsPrincipal principal,
        HttpContext httpContext,
        [FromServices] PlayerIdentityService identityService,
        [FromServices] UserService userService,
        CancellationToken cancellationToken) =>
        HandleAsync(httpContext, async () =>
        {
            var caller = await GetCallerAsync(principal, userService);
            var identities = caller.IsAdministrator ? null : await identityService.GetForUserAsync(caller.UserId!.Value, cancellationToken);

            if (identities != null && identities.All(i => i.Id != identityId))
                return Forbidden();

            return await identityService.DeleteAsync(identityId, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
        });

    /// <summary>
    /// The caller as a reporter. Trust comes from the account: administrators are authoritative,
    /// everyone else is a player's own client. A token's scopes only narrow what it reaches.
    /// </summary>
    internal static async Task<MatchReporterContext> GetCallerAsync(ClaimsPrincipal principal, UserService userService)
    {
        var isAdministrator = principal.IsInRole(RoleService.AdministratorRoleName);

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var user = await userService.GetAsync(principal.Identity?.Name)
                ?? throw new MatchAccessDeniedException("The caller isn't a user");

            userId = user.Id;
        }

        return new MatchReporterContext(
            userId,
            null,
            isAdministrator ? MatchReporterTrust.Authoritative : MatchReporterTrust.Untrusted,
            isAdministrator);
    }

    // A problem response rather than Forbid(), which would challenge with the cookie scheme
    private static IResult Forbidden() =>
        TypedResults.Problem("You can only manage your own player identities", statusCode: StatusCodes.Status403Forbidden);

    /// <summary>Turns the match services' exceptions into responses.</summary>
    private static async Task<IResult> HandleAsync(HttpContext httpContext, Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MatchStatisticsDisabledException)
        {
            return TypedResults.NotFound();
        }
        catch (MatchQueueFullException)
        {
            httpContext.Response.Headers.RetryAfter = "2";

            return TypedResults.Problem("The server is behind on writing match events. Retry with the same event ids.", statusCode: StatusCodes.Status429TooManyRequests);
        }
        catch (MatchAccessDeniedException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
