using System.Net.Mime;
using System.Security.Claims;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace LANCommander.Server.Endpoints;

public static class IssueEndpoints
{
    // Matches the MaxLength on Issue.Title
    private const int MaxTitleLength = 128;

    public static void MapIssueEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Issue").RequireAuthorization();

        group.MapPost("/Open", OpenAsync);
        group.MapPost("/", CreateAsync).DisableAntiforgery();
        group.MapGet("/Game/{gameId:guid}", GetByGameAsync);
        group.MapGet("/Limits", GetLimits);
        group.MapGet("/Attachments/{id:guid}/Download", DownloadAttachmentAsync);
        group.MapGet("/", GetAsync).RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapPost("/{id:guid}/Resolve", ResolveByIdAsync).RequireAuthorization(RoleService.AdministratorRoleName);
    }

    internal static async Task<IResult> OpenAsync(
        [FromServices] IssueService issueService,
        [FromServices] GameService gameService,
        [FromServices] ILogger<Issue> logger,
        SDK.Models.Issue issueRequest)
    {
        try
        {
            var game = await gameService.GetAsync(issueRequest.GameId);

            if (game != null)
            {
                var title = issueRequest.Title?.Trim();

                if (title?.Length > MaxTitleLength)
                    title = title[..MaxTitleLength];

                var issue = new Issue
                {
                    GameId = game.Id,
                    Title = string.IsNullOrEmpty(title) ? null : title,
                    Description = issueRequest.Description ?? string.Empty,
                };

                await issueService.AddAsync(issue);

                // IssueClient.Open deserializes the body as bool; an empty 200 reads as false
                return TypedResults.Ok(true);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not open new issue");
        }

        return TypedResults.BadRequest();
    }

    /// <summary>
    /// Opens an issue from a multipart form (<c>gameId</c>, <c>title</c>, <c>description</c> and any
    /// number of <c>attachments</c> files) and returns it. Every file is checked against
    /// <see cref="IssueAttachmentRules"/> before anything is saved.
    /// </summary>
    internal static async Task<IResult> CreateAsync(
        HttpRequest request,
        ClaimsPrincipal userPrincipal,
        [FromServices] IssueService issueService,
        [FromServices] IssueAttachmentService attachmentService,
        [FromServices] GameService gameService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ILogger<Issue> logger)
    {
        if (!request.HasFormContentType)
            return TypedResults.BadRequest("Send the issue as a multipart form.");

        var form = await request.ReadFormAsync();

        if (!Guid.TryParse(form["gameId"], out var gameId))
            return TypedResults.BadRequest("The issue doesn't say which game it is for.");

        var title = form["title"].ToString().Trim();
        var description = form["description"].ToString().Trim();

        if (string.IsNullOrEmpty(title))
            return TypedResults.BadRequest("Give the issue a title.");

        if (title.Length > MaxTitleLength)
            title = title[..MaxTitleLength];

        var files = form.Files.GetFiles("attachments");
        var problem = IssueAttachmentRules.Validate(
            files.Select(f => (f.FileName, f.Length)).ToList(),
            GetAttachmentLimits(settingsProvider.CurrentValue));

        if (problem != null)
            return TypedResults.BadRequest(problem);

        if (!await CanSeeGameAsync(gameService, userPrincipal, gameId))
            return TypedResults.NotFound();

        var issue = await issueService.AddAsync(new Issue
        {
            GameId = gameId,
            Title = title,
            Description = description,
        });

        var attachments = new List<IssueAttachment>();

        try
        {
            foreach (var file in files)
            {
                await using var stream = file.OpenReadStream();

                attachments.Add(await attachmentService.AddAsync(issue.Id, file.FileName, stream));
            }
        }
        catch (Exception ex)
        {
            // Don't leave a report behind without the files the user meant to send with it
            logger.LogError(ex, "Could not save the attachments for issue {IssueId}", issue.Id);

            await issueService.DeleteAsync(issue);

            return TypedResults.Problem("The server could not save the attached files.");
        }

        return TypedResults.Ok(new SDK.Models.Issue
        {
            Id = issue.Id,
            GameId = issue.GameId,
            Title = issue.Title,
            Description = issue.Description,
            IsPinned = issue.IsPinned,
            ResolvedOn = issue.ResolvedOn,
            CreatedOn = issue.CreatedOn,
            CreatedBy = userPrincipal.Identity?.Name,
            Attachments = attachments.Select(ToSdk).ToList(),
        });
    }

    /// <summary>
    /// Sends an attached file. Its content type comes from <see cref="IssueAttachmentRules"/>, not
    /// from the upload, and only images and plain text are shown inline.
    /// </summary>
    internal static async Task<IResult> DownloadAttachmentAsync(
        HttpResponse response,
        ClaimsPrincipal userPrincipal,
        [FromServices] IssueAttachmentService attachmentService,
        [FromServices] IssueService issueService,
        [FromServices] GameService gameService,
        Guid id)
    {
        var attachment = await attachmentService.GetAsync(id);

        if (attachment == null)
            return TypedResults.NotFound();

        var issue = await issueService.GetAsync(attachment.IssueId);

        if (issue == null || !await CanSeeGameAsync(gameService, userPrincipal, issue.GameId))
            return TypedResults.NotFound();

        var path = attachmentService.GetPath(attachment);

        if (!File.Exists(path))
            return TypedResults.NotFound();

        var contentType = IssueAttachmentRules.GetContentType(attachment.FileName);
        var inline = contentType != MediaTypeNames.Application.Octet;

        response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
        {
            FileNameStar = attachment.FileName,
        }.ToString();

        return TypedResults.File(File.OpenRead(path), contentType, enableRangeProcessing: true);
    }

    /// <summary>The attachment limits set on the Issues settings page, for clients to check against.</summary>
    internal static IResult GetLimits([FromServices] SettingsProvider<Settings.Settings> settingsProvider)
        => TypedResults.Ok(GetAttachmentLimits(settingsProvider.CurrentValue));

    internal static SDK.Models.IssueAttachmentLimits GetAttachmentLimits(Settings.Settings settings) => new()
    {
        MaxFiles = Math.Max(0, settings.Server.Issues.MaxAttachments),
        MaxFileSize = Math.Max(1, settings.Server.Issues.MaxAttachmentSize) * 1024L * 1024L,
    };

    private static SDK.Models.IssueAttachment ToSdk(IssueAttachment attachment) => new()
    {
        Id = attachment.Id,
        IssueId = attachment.IssueId,
        FileName = attachment.FileName,
        Size = attachment.Size,
    };

    /// <summary>Unpublished games, and so their issues, are only visible to administrators.</summary>
    private static async Task<bool> CanSeeGameAsync(GameService gameService, ClaimsPrincipal userPrincipal, Guid gameId)
    {
        if (await gameService.GetAsync(gameId) == null)
            return false;

        if (userPrincipal.IsInRole(RoleService.AdministratorRoleName))
            return true;

        return !(await gameService.GetUnpublishedGameIdsAsync()).Contains(gameId);
    }

    /// <summary>
    /// Lists a game's issues for any signed-in user: pinned first, then unresolved, newest first.
    /// Issues on unpublished games are hidden from non-administrators, as the games themselves are.
    /// </summary>
    internal static async Task<IResult> GetByGameAsync(
        [FromServices] IssueService issueService,
        [FromServices] GameService gameService,
        ClaimsPrincipal userPrincipal,
        Guid gameId)
    {
        if (!userPrincipal.IsInRole(RoleService.AdministratorRoleName))
        {
            var unpublishedIds = await gameService.GetUnpublishedGameIdsAsync();

            if (unpublishedIds.Contains(gameId))
                return TypedResults.NotFound();
        }

        var issues = await issueService
            .GetAsync(i => i.GameId == gameId, query => query
                .OrderByDescending(i => i.IsPinned)
                .ThenBy(i => i.ResolvedOn != null)
                .ThenByDescending(i => i.CreatedOn)
                .Select(i => new SDK.Models.Issue
                {
                    Id = i.Id,
                    GameId = i.GameId,
                    Title = i.Title,
                    Description = i.Description,
                    IsPinned = i.IsPinned,
                    ResolvedOn = i.ResolvedOn,
                    CreatedOn = i.CreatedOn,
                    CreatedBy = i.CreatedBy != null ? i.CreatedBy.UserName : null,
                    Attachments = i.Attachments!
                        .OrderBy(a => a.CreatedOn)
                        .Select(a => new SDK.Models.IssueAttachment
                        {
                            Id = a.Id,
                            IssueId = a.IssueId,
                            FileName = a.FileName,
                            Size = a.Size,
                        })
                        .ToList(),
                }));

        return TypedResults.Ok(issues);
    }

    internal static async Task<IResult> GetAsync(
        [FromServices] IssueService issueService)
    {
        var issues = await issueService.GetAsync();
        
        return TypedResults.Ok(issues);
    }

    internal static async Task<IResult> GetByIdAsync(
        [FromServices] IssueService issueService,
        Guid id)
    {
        var issue = await issueService.GetAsync(id);
        
        if (issue != null)
            return TypedResults.Ok(issue);
        
        return TypedResults.NotFound();
    }

    internal static async Task<IResult> ResolveByIdAsync(
        [FromServices] IssueService issueService,
        [FromServices] UserService userService,
        ClaimsPrincipal userPrincipal,
        Guid id)
    {
        var issue = await issueService.GetAsync(id);
        var user = await userService.GetAsync(userPrincipal.Identity.Name);
        
        if (issue == null)
            return TypedResults.NotFound();
        
        await issueService.ResolveAsync(id, user.Id);

        return TypedResults.Ok();
    }
}