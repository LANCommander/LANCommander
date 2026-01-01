using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using HubChatClient = LANCommander.SDK.Hubs.IChatClient;
using LANCommander.SDK.Hubs;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services;

public class ChatClient : IChatClient, HubChatClient
{
    private HubConnection? _connection;
    private IChatHub? _hub;
    private readonly ITokenProvider _tokenProvider;
    private readonly IConnectionClient _connectionClient;
    private readonly ILogger<ChatClient> _logger;
    private readonly Dictionary<Guid, ChatThread> _threads = new();

    public ChatClient(
        ITokenProvider tokenProvider,
        IConnectionClient connectionClient,
        ILogger<ChatClient> logger)
    {
        _tokenProvider = tokenProvider;
        _connectionClient = connectionClient;
        _logger = logger;

        // Subscribe to connection events to manage chat hub connection
        _connectionClient.OnConnect += async (sender, e) => await EnsureConnectedAsync();
        _connectionClient.OnDisconnect += async (sender, e) => await DisconnectAsync();
    }

    private async Task EnsureConnectedAsync()
    {
        if (_connection?.State == HubConnectionState.Connected)
            return;

        var serverAddress = _connectionClient.GetServerAddress();
        if (serverAddress == null)
            return;

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(serverAddress.Join("hubs/chat"), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_tokenProvider.GetToken().AccessToken);
                })
                .WithAutomaticReconnect()
                .Build();

            _hub = _connection.ServerProxy<IChatHub>();
            _ = _connection.ClientRegistration<HubChatClient>(this);

            await _connection.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to chat hub at {ServerAddress}", serverAddress);
        }
    }

    private async Task DisconnectAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync();
                await _connection.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to disconnect from chat hub");
            }
            finally
            {
                _connection = null;
                _hub = null;
            }
        }
    }

    public async Task<ChatThread> GetThreadAsync(Guid threadId)
    {
        return _threads[threadId];
    }

    public async Task<Guid> StartThreadAsync(IEnumerable<string> userIdentifiers)
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        var threadId = await _hub.StartThreadAsync(userIdentifiers.ToArray());

        if (threadId != Guid.Empty)
        {
            _threads[threadId] = await _hub.GetThreadAsync(threadId);
        }

        return threadId;
    }

    public async Task AddedToThreadAsync(ChatThread thread)
    {
        _threads[thread.Id] = thread;
    }
    
    public async Task<IEnumerable<ChatThread>> GetThreadsAsync()
    {
        if (_threads.Count == 0)
            return await LoadThreadsAsync();
        
        return _threads.Values.ToArray();
    }

    public async Task<IEnumerable<ChatThread>> LoadThreadsAsync()
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        var threads = await _hub.GetThreadsAsync();

        _threads.Clear();

        foreach (var thread in threads)
            _threads[thread.Id] = thread;

        return threads;
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        return await _hub.GetUsersAsync();
    }
    
    public async Task ReceiveMessagesAsync(Guid threadId, IEnumerable<ChatMessage> messages)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessagesReceivedAsync(messages);
    }

    public async Task ReceiveMessageAsync(Guid threadId, ChatMessage message)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.MessageReceivedAsync(message);
    }

    public async Task StartTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StartTypingAsync(userId);
    }

    public async Task StopTypingAsync(Guid threadId, string userId)
    {
        if (_threads.TryGetValue(threadId, out var thread))
            await thread.StopTypingAsync(userId);
    }

    public async Task SendMessageAsync(Guid threadId, string message)
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        if (_threads.TryGetValue(threadId, out var thread))
            await _hub.SendMessageAsync(thread.Id, message);
    }

    public async Task UpdatedReadStatus(Guid threadId)
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        if (_threads.TryGetValue(threadId, out var thread))
            await _hub.UpdateReadStatusAsync(thread.Id);
    }

    public async Task GetMessagesAsync(Guid threadId)
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        await _hub.GetMessagesAsync(threadId);
    }

    public async Task<int> GetUnreadMessageCountAsync(Guid threadId)
    {
        await EnsureConnectedAsync();

        if (_hub == null)
            throw new InvalidOperationException("Chat hub is not connected");

        return await _hub.GetUnreadMessageCountAsync(threadId);
    }
}