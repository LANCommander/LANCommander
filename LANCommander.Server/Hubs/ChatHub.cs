using AutoMapper;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class ChatHub(
        IMapper mapper,
        ChatService chatService) : Hub
    {
        public async Task GetMessages(Guid threadId)
        {
            var messages = chatService.GetMessagesAsync(threadId);
            
            await Clients.Caller.SendAsync("ReceiveMessages", threadId, messages);
        }

        public async Task SendMessage(Guid threadId, string content)
        {
            var message = await chatService.SendMessageAsync(threadId, content);
            
            await Clients.Group($"Chat/Thread/{threadId}").SendAsync("ReceiveMessages", threadId, new[] { mapper.Map<ChatMessage>(message) });
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionAborted}");
            await Clients.Caller.SendAsync("OnConnected", Context.ConnectionId);
            
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected {Context.ConnectionId}");
        }
    }
}
