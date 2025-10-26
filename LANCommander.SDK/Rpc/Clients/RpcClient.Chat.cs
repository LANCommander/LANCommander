using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Rpc.Clients;

internal partial class RpcSubscriber : IRpcSubscriber
{
    public async Task Chat_AddedToThreadAsync(ChatThread thread)
    {
        if (ChatClient.OnAddedToThreadAsync != null)
            await ChatClient.OnAddedToThreadAsync(thread);
    }

    public async Task Chat_ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages)
    {
        if (ChatClient.OnReceivedMessagesAsync != null)
            await ChatClient.OnReceivedMessagesAsync(threadId, messages);
    }

    public async Task Chat_ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        if (ChatClient.OnReceivedMessageAsync != null)
            await ChatClient.OnReceivedMessageAsync(threadId, message);
    }

    public async Task Chat_StartTyping(Guid threadId, string userIdentifier)
    {
        if (ChatClient.OnStartTypingAsync != null)
            await ChatClient.OnStartTypingAsync(threadId, userIdentifier);
    }

    public async Task Chat_StopTyping(Guid threadId, string userIdentifier)
    {
        if (ChatClient.OnStopTypingAsync != null)
            await ChatClient.OnStopTypingAsync(threadId, userIdentifier);
    }
}