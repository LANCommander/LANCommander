using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient
{
    private readonly ChatClient _chatClient = serviceProvider.GetService<ChatClient>();

    public async Task Chat_AddedToThreadAsync(ChatThread thread)
    {
        await _chatClient.AddedToThreadAsync(thread);
    }

    public async Task Chat_ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages)
    {
        await _chatClient.ReceiveMessagesAsync(threadId, messages);
    }

    public async Task Chat_ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        await _chatClient.ReceiveMessageAsync(threadId, message);
    }

    public async Task Chat_StartTyping(Guid threadId, string userIdentifier)
    {
        await _chatClient.StartTypingAsync(threadId, userIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId, string userIdentifier)
    {
        await _chatClient.StopTypingAsync(threadId, userIdentifier);
    }
}