using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Rpc.Server;

public partial interface IRpcHub
{
    Task<Guid> Chat_StartThreadAsync(string[] userIdentifiers);
    Task Chat_AddParticipantAsync(Guid threadId, string participantId);
    Task<ChatThread> Chat_GetThreadAsync(Guid threadId);
    Task<IEnumerable<ChatThread>> Chat_GetThreadsAsync();
    Task Chat_SendMessageAsync(Guid threadId, string message);
    Task Chat_GetMessagesAsync(Guid threadId);
    Task Chat_StartTyping(Guid threadId);
    Task Chat_StopTyping(Guid threadId);
    Task<IEnumerable<User>> Chat_GetUsersAsync();
}