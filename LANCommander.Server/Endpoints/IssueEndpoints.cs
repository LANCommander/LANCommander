using System.Net.Mime;
using System.Security.Claims;
using LANCommander.SDK.Services;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class IssueEndpoints
{
    public static void MapIssueEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Issue").RequireAuthorization();

        group.MapPost("/Open", OpenAsync);
        group.MapGet("/", GetAsync).RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(RoleService.AdministratorRoleName);
        group.MapPost("/{id:guid}/Resolve", ResolveByIdAsync).RequireAuthorization(RoleService.AdministratorRoleName);
    }

    internal static async Task<IResult> OpenAsync(
        [FromServices] IssueService issueService,
        [FromServices] GameService gameService,
        [FromServices] ILogger logger,
        SDK.Models.Issue issueRequest)
    {
        try
        {
            var game = await gameService.GetAsync(issueRequest.GameId);

            if (game != null)
            {
                var issue = new Issue
                {
                    GameId = game.Id,
                    Description = game.Description,
                };

                await issueService.AddAsync(issue);

                return TypedResults.Ok();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not open new issue");
        }

        return TypedResults.BadRequest();
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