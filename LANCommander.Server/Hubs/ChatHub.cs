using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Hubs
{
    public class ChatHub(
        IMapper mapper,
        IFusionCache cache,
        ChatService chatService) : Hub
    {
        private string GetThreadGroupName(Guid threadId) => $"Chat/Thread/{threadId}";
        private string GetConnectionsCacheKey(string userIdentifier) => $"Chat/Connections/{userIdentifier}";

        public async Task StartThread(IEnumerable<string> userIdentifiers)
        {
            var thread = await chatService.StartThreadAsync();
            
            foreach (var userIdentifier in userIdentifiers)
                await AddParticipant(thread.Id, userIdentifier);
        }

        public async Task AddParticipant(Guid threadId, string participantId)
        {
            var cacheKey = GetConnectionsCacheKey(participantId);

            var connections = await cache.TryGetAsync<List<string>>(cacheKey);
            
            if (connections.HasValue)
                foreach (var connection in connections.Value)
                    await Groups.AddToGroupAsync(connection, threadId.ToString());
        }

        public async Task SendMessage(Guid threadId, string content)
        {
            var message = await chatService.SendMessageAsync(threadId, content);
            
            await Clients.Group(GetThreadGroupName(threadId)).SendAsync("ReceiveMessages", threadId, new[] { mapper.Map<ChatMessage>(message) });
        }
        
        public async Task GetMessages(Guid threadId)
        {
            var messages = chatService.GetMessagesAsync(threadId);
            
            await Clients.Caller.SendAsync("ReceiveMessages", threadId, messages);
        }

        public async Task StartTyping(Guid threadId)
        {
            await Clients.Group(GetThreadGroupName(threadId)).SendAsync("StartTyping", threadId, Context.UserIdentifier);
        }

        public async Task StopTyping(Guid threadId)
        {
            await Clients.Group(GetThreadGroupName(threadId)).SendAsync("StopTyping", threadId);
        }

        public override async Task OnConnectedAsync()
        {
            var cacheKey = GetConnectionsCacheKey(Context.UserIdentifier);
            
            var connections = await cache.TryGetAsync<List<string>>(cacheKey);

            connections = connections.HasValue
                ? new List<string>(connections.Value)
                : new List<string>();

            connections.Value.RemoveAll(c => c == Context.ConnectionId);
            connections.Value.Add(Context.ConnectionId);

            await cache.SetAsync(cacheKey, connections);
            
            await Clients.Caller.SendAsync("OnConnected", Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var cacheKey = GetConnectionsCacheKey(Context.UserIdentifier);
            
            var connections = await cache.TryGetAsync<List<string>>(cacheKey);

            if (connections.HasValue)
            {
                connections.Value.RemoveAll(c => c == Context.ConnectionId);
                
                await cache.SetAsync(cacheKey, connections);
            }
        }
    }
}
