using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient
{
    private readonly ChatService _chatService = serviceProvider.GetService<ChatService>();

    public async Task Chat_AddedToThreadAsync(ChatThread thread)
    {
        await _chatService.AddedToThreadAsync(thread);
    }

    public async Task Chat_ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages)
    {
        await _chatService.ReceiveMessagesAsync(threadId, messages);
    }

    public async Task Chat_ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        await _chatService.ReceiveMessageAsync(threadId, message);
    }

    public async Task Chat_StartTyping(Guid threadId, string userIdentifier)
    {
        await _chatService.StartTypingAsync(threadId, userIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId, string userIdentifier)
    {
        await _chatService.StopTypingAsync(threadId, userIdentifier);
    }
}