using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Chat");
        
        group.MapPost("/Thread", StartThreadAsync);
        group.MapGet("/Thread/{id:guid}", GetByThreadIdAsync);
        group.MapPost("/Thread/{id:guid}/Message", SendMessageAsync);
    }

    public static async Task<IResult> StartThreadAsync(
        string content,
        [FromServices] ChatThreadService chatThreadService)
    {
        return TypedResults.Ok(await chatThreadService.AddAsync(new ChatThread()));
    }

    public static async Task<IResult> GetByThreadIdAsync(
        Guid threadId,
        [FromServices] ChatService chatService,
        [FromServices] IMapper mapper)
    {
        var messages = mapper.Map<IEnumerable<SDK.Models.ChatMessage>>(await chatService.GetMessagesAsync(threadId, 10));
        
        return TypedResults.Ok(messages);
    }

    public static async Task<IResult> SendMessageAsync(
        Guid threadId,
        string content,
        [FromServices] ChatService chatService)
    {
        await chatService.SendMessageAsync(threadId, content);
        
        return TypedResults.Ok();
    }
}