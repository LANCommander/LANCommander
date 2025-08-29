using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Rpc;

public partial class RpcClient
{
    public async Task Chat_AddedToThreadAsync(ChatThread thread)
    {
        await client.Chat.AddedToThreadAsync(thread);
    }

    public async Task Chat_ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages)
    {
        await client.Chat.ReceiveMessagesAsync(threadId, messages);
    }

    public async Task Chat_ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        await client.Chat.ReceiveMessageAsync(threadId, message);
    }

    public async Task Chat_StartTyping(Guid threadId, string userIdentifier)
    {
        await client.Chat.StartTypingAsync(threadId, userIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId, string userIdentifier)
    {
        await client.Chat.StopTypingAsync(threadId, userIdentifier);
    }
}