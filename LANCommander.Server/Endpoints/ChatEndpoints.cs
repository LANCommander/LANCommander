using System.Security.Claims;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Chat").RequireAuthorization();

        group.MapPost("/Thread", StartThreadAsync);
        group.MapGet("/Thread/{id:guid}", GetByThreadIdAsync);
        group.MapPost("/Thread/{id:guid}/Message", SendMessageAsync);
    }

    static Guid? GetUserId(ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    public static async Task<IResult> StartThreadAsync(
        ClaimsPrincipal user,
        [FromServices] ChatService chatService)
    {
        if (GetUserId(user) is not { } userId)
            return TypedResults.Unauthorized();

        var thread = await chatService.StartThreadAsync();

        await chatService.AddParticipantAsync(thread.Id, userId);

        return TypedResults.Ok(thread.Id);
    }

    public static async Task<IResult> GetByThreadIdAsync(
        Guid id,
        ClaimsPrincipal user,
        [FromServices] ChatService chatService)
    {
        if (GetUserId(user) is not { } userId || !await chatService.IsParticipantAsync(id, userId))
            return TypedResults.Forbid();

        var messages = (await chatService.GetMessagesAsync(id, 10)).Items;

        return TypedResults.Ok(messages);
    }

    public static async Task<IResult> SendMessageAsync(
        Guid id,
        string content,
        ClaimsPrincipal user,
        [FromServices] ChatService chatService,
        [FromServices] ChatBroadcaster broadcaster,
        [FromServices] SdkMapper sdkMapper)
    {
        if (GetUserId(user) is not { } userId || !await chatService.IsParticipantAsync(id, userId))
            return TypedResults.Forbid();

        var message = await chatService.SendMessageAsync(id, userId, content);
        var participants = await chatService.GetParticipantIdsAsync(id);

        await broadcaster.MessageSentAsync(id, sdkMapper.ToSdk(message), participants);

        return TypedResults.Ok();
    }
}
