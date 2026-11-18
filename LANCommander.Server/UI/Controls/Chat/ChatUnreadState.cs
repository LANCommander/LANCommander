using LANCommander.Server.Clients;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// One browser tab's view of chat: how many messages it hasn't read, for the sidebar and the tab's
/// badge, and which conversation it has open, so a message there isn't announced as new.
/// </summary>
public sealed class ChatUnreadState(ServerChatClient chatClient, ILogger<ChatUnreadState> logger)
{
    /// <summary>Unread messages across every thread the user is in</summary>
    public int UnreadCount { get; private set; }

    /// <summary>The thread the chat page is showing, if it's open</summary>
    public Guid? OpenThreadId { get; set; }

    /// <summary>Raised when <see cref="UnreadCount"/> changes</summary>
    public event Action? Changed;

    public async Task RefreshAsync()
    {
        try
        {
            var count = 0;

            foreach (var thread in await chatClient.GetThreadsAsync())
                count += await chatClient.GetUnreadMessageCountAsync(thread.Id);

            if (count == UnreadCount)
                return;

            UnreadCount = count;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not count the unread chat messages");

            return;
        }

        Changed?.Invoke();
    }
}
