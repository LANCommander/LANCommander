using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.SDK.Tests.Chat;

public class ChatClientTests
{
    [Fact]
    public async Task AThreadAddedAgainKeepsTheInstanceItsHandlersAreOn()
    {
        using var client = new ChatClient(new NoTokenProvider(), new OfflineConnectionClient(), NullLogger<ChatClient>.Instance);
        var added = new List<ChatThread>();
        client.ThreadAdded += added.Add;

        var threadId = Guid.NewGuid();
        var alice = new User { Id = Guid.NewGuid(), UserName = "alice" };
        var bob = new User { Id = Guid.NewGuid(), UserName = "bob" };

        var original = new ChatThread { Id = threadId, Participants = [alice] };
        await client.AddedToThreadAsync(original);

        var received = new List<string>();
        original.OnMessageReceivedAsync = message =>
        {
            received.Add(message.Content);

            return Task.CompletedTask;
        };

        // Reopened from elsewhere: the server announces the thread again as a fresh copy
        await client.AddedToThreadAsync(new ChatThread { Id = threadId, Participants = [alice, bob] });

        await client.ReceiveMessageAsync(threadId, new ChatMessage { Id = Guid.NewGuid(), UserId = bob.Id, UserName = bob.UserName, SentOn = DateTimeOffset.UtcNow, Content = "Still heard" });

        Assert.Same(original, Assert.Single(added));
        Assert.Same(original, await client.GetThreadAsync(threadId));
        Assert.Equal(["Still heard"], received);
        Assert.Equal([alice.Id, bob.Id], original.Participants.Select(p => p.Id));
    }

    sealed class NoTokenProvider : ITokenProvider
    {
        public void SetToken(AuthToken token) { }
        public AuthToken GetToken() => new();
    }

    sealed class OfflineConnectionClient : IConnectionClient
    {
        public event EventHandler? OnConnect { add { } remove { } }
        public event EventHandler? OnDisconnect { add { } remove { } }
        public event EventHandler? OnServerAddressChanged { add { } remove { } }
        public event EventHandler? OnOfflineModeEnabled { add { } remove { } }

        public bool IsConnected() => false;
        public bool IsConfigured() => false;
        public bool IsOfflineMode() => true;
        public bool HasServerAddress() => false;
        public Uri GetServerAddress() => null!;
        public Task UpdateServerAddressAsync(string address) => Task.CompletedTask;
        public Task UpdateServerAddressAsync(Uri address) => Task.CompletedTask;
        public Task<bool> ConnectAsync() => Task.FromResult(false);
        public Task<bool> DisconnectAsync() => Task.FromResult(true);
        public Task EnableOfflineModeAsync() => Task.CompletedTask;
        public Task<bool> PingAsync(Uri? serverAddress = null) => Task.FromResult(false);
    }
}
