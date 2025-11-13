using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Rpc.Server;

namespace LANCommander.SDK.Services;

// Base Client
public class RpcClient(IRpcSubscriber subscriber)
{
    internal static IRpcHub Hub { get; set; }
    
    IRpcSubscriber _subscriber = subscriber;
    
    public RpcChatClient Chat => new(_subscriber);
    public RpcServerClient Servers => new(_subscriber);

    public bool IsConnected => _subscriber.IsConnected();

    public async Task ConnectAsync(Uri address)
        => await _subscriber.ConnectAsync(address);
    
    public async Task<bool> DisconnectAsync()
        => await _subscriber.DisconnectAsync();
}

public class RpcChatClient(IRpcSubscriber subscriber)
{
    public async Task<Guid> StartThreadAsync(params string[] userIdentifiers)
        => await RpcClient.Hub.Chat_StartThreadAsync(userIdentifiers);
    
    public async Task AddParticipantAsync(Guid threadId, string participantId)
        => await RpcClient.Hub.Chat_AddParticipantAsync(threadId, participantId);
    
    public async Task<ChatThread> GetThreadAsync(Guid threadId)
        => await RpcClient.Hub.Chat_GetThreadAsync(threadId);
    
    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
        => await RpcClient.Hub.Chat_GetThreadsAsync();
    
    public async Task SendMessageAsync(Guid threadId, string message)
        => await RpcClient.Hub.Chat_SendMessageAsync(threadId, message);
    
    public async Task GetMessagesAsync(Guid threadId)
        => await RpcClient.Hub.Chat_GetMessagesAsync(threadId);

    public async Task StartTypingAsync(Guid threadId)
        => await RpcClient.Hub.Chat_StartTyping(threadId);
    
    public async Task StopTypingAsync(Guid threadId)
        => await RpcClient.Hub.Chat_StopTyping(threadId);
    
    public async Task UpdateReadStatusAsync(Guid threadId)
        => await RpcClient.Hub.Chat_UpdateReadStatus(threadId);
    
    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
        => await RpcClient.Hub.Chat_GetUnreadMessageCountAsync(threadId);
    
    public async Task<IEnumerable<User>> GetUsersAsync()
        => await RpcClient.Hub.Chat_GetUsersAsync();
}

public class RpcServerClient(IRpcSubscriber subscriber)
{
    public async Task GetStatusAsync(Guid serverId)
        => await RpcClient.Hub.Server_GetStatusAsync(serverId);
    
    public async Task UpdateStatusAsync(Guid serverId)
        => await RpcClient.Hub.Server_UpdateStatusAsync(serverId);
    
    public async Task StartAsync(Guid serverId)
        => await RpcClient.Hub.Server_StartAsync(serverId);
    
    public async Task StopAsync(Guid serverId)
        => await RpcClient.Hub.Server_StopAsync(serverId);
    
    public async Task LogAsync(Guid serverId, string message)
        => await RpcClient.Hub.Server_LogAsync(serverId, message);
}