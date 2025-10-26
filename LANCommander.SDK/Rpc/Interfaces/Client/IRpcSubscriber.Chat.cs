using System;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcSubscriber
{
    Task Chat_AddedToThreadAsync(ChatThread thread);
    Task Chat_ReceiveMessagesAsync(Guid threadId, ChatMessage[] messages);
    Task Chat_ReceiveMessageAsync(Guid threadId, ChatMessage message);
    Task Chat_StartTyping(Guid threadId, string userIdentifier);
    Task Chat_StopTyping(Guid threadId, string userIdentifier);
}