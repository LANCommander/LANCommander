using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

public class ChatService
{
    private readonly Client _client;
    private readonly IList<ChatThread> _threads = new List<ChatThread>();

    public ChatService(Client client)
    {
        _client = client;
    }
    
    public static IEnumerable<ChatMessageGroup> GroupConsecutiveMessages(IEnumerable<ChatMessage> source,
        TimeSpan? maxGap = null)
    {
        ChatMessageGroup? current = null;
        ChatMessage? last = null;

        foreach (var message in source.OrderBy(x => x.SentOn))
        {
            var mustBreak = current is null || message.UserId != current.UserId || (maxGap is not null &&
                last is not null && (message.SentOn - last.SentOn) > maxGap.Value);

            if (mustBreak)
            {
                if (current is not null)
                    yield return current;

                current = new ChatMessageGroup
                {
                    UserId = message.UserId,
                    UserName = message.UserName,
                    Messages = [message],
                };
            }
            else
                current!.Messages.Add(message);

            last = message;
        }
        
        if (current is not null)
            yield return current;
    }
    
    public async Task<IEnumerable<ChatMessage>> GetMessagesAsync(Guid threadId)
    {
        return await _client.GetRequestAsync<IEnumerable<ChatMessage>>($"/api/Chat/Thread/{threadId}");
    }

    public async Task<ChatThread> StartThreadAsync()
    {
        return await _client.PostRequestAsync<ChatThread>("/api/Chat/Thread");
    }

    public async Task<ChatMessage> SendMessageAsync(Guid threadId, string content)
    {
        return await _client.PostRequestAsync<ChatMessage>($"/api/Chat/Thread/{threadId}/Message", content);
    }

    public async Task Connect(Guid threadId)
    {
        
    }
}