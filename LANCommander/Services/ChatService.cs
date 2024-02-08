using LANCommander.Data;
using LANCommander.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Services
{
    public class ChatService
    {
        private readonly IServiceScopeFactory ScopeFactory;
        private Dictionary<string, User> Connections { get; set; }

        public ChatService(IServiceScopeFactory scopeFactory)
        {
            ScopeFactory = scopeFactory;
            Connections = new Dictionary<string, User>();
        }

        public async Task Connect(string connectionId, string username)
        {
            using (var scope = ScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                var user = await context.Users.FirstOrDefaultAsync(u => u.UserName == username);

                if (user == null)
                    throw new UnauthorizedAccessException();

                Connections[connectionId] = user;
            }
        }

        public User GetUser(string connectionId)
        {
            return Connections[connectionId];
        }

        public async Task<Message> SendMessageToUserAsync(Guid senderId, Guid recipientId, string contents)
        {
            using (var scope = ScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                var sender = await context.Users.FirstOrDefaultAsync(u => u.Id == senderId);
                var recipient = await context.Users.FirstOrDefaultAsync(u => u.Id == recipientId);

                var channel = sender.Channels.FirstOrDefault(c => c.Users.Count() == 2 && c.Users.Any(u => u.Id == recipientId));

                // Create DM channel if it doesn't already exist
                if (channel == null)
                {
                    channel = new Channel
                    {
                        Type = Data.Enums.ChannelType.DirectMessage,
                        CreatedBy = sender,
                        Users = new List<User> { sender, recipient },
                        Name = "Direct Message",
                        CreatedOn = DateTime.Now
                    };

                    await context.AddAsync(channel);
                }

                var message = new Message
                {
                    Channel = channel,
                    Contents = contents,
                    CreatedBy = sender,
                    Sender = sender,
                    CreatedOn = DateTime.Now
                };

                await context.AddAsync(message);
                await context.SaveChangesAsync();

                return message;
            }
        }

        public async Task<IEnumerable<Message>> GetMessages(Guid channelId, int skip = 0, int take = 25)
        {
            using (var scope = ScopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DatabaseContext>();

                return await context.Messages.Where(m => m.Channel.Id == channelId).OrderByDescending(m => m.CreatedOn).Skip(skip).Take(take).ToListAsync();
            }
        }
    }
}
