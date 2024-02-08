using LANCommander.Data.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        public static ChatService ChatService;
        
        public ChatHub(ChatService chatService)
        {
            ChatService = chatService;
        }

        public async Task Connect()
        {
            try
            {
                await ChatService.Connect(Context.ConnectionId, Context.User.Identity.Name);
            }
            catch (Exception ex)
            {

            }
        }

        public async Task SendMessage(Guid recipient, string message)
        {
            try
            {
                var result = await ChatService.SendMessageToUserAsync(ChatService.GetUser(Context.ConnectionId).Id, recipient, message);

                await Clients.Users(recipient.ToString()).SendAsync("ReceiveMessage", result);
            }
            catch (Exception ex)
            {

            }
        }

        public async Task<IEnumerable<Message>> GetMessages(Guid channelId, int skip = 0, int take = 25)
        {
            return await ChatService.GetMessages(channelId, skip, take);
        }
    }
}
