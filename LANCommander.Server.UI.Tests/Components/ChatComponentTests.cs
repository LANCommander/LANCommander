using System.Security.Claims;
using Bunit;
using LANCommander.SDK.Services;
using LANCommander.Server.Clients;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Hubs;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using LANCommander.Server.UI.Controls;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit tests for chat in the server UI: threads shown live alongside launchers, which send
/// through the chat hub, and messages sent from the browser reaching the same broadcaster.
/// </summary>
[Collection("BUnit")]
public class ChatComponentTests : BUnitTestContext
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public ChatComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
        // Built in bUnit's container so it reads bUnit's signed-in user rather than the server's
        // circuit authentication, which has no request to read from here
        Services.AddScoped(provider => new ServerChatClient(
            Services.GetRequiredService<ChatService>(),
            Services.GetRequiredService<ChatBroadcaster>(),
            provider.GetRequiredService<AuthenticationStateProvider>(),
            Services.GetRequiredService<SdkMapper>(),
            NullLogger<ServerChatClient>.Instance));
        Services.AddScoped<IChatClient>(provider => provider.GetRequiredService<ServerChatClient>());
        Services.AddScoped(provider => new ChatUnreadState(
            provider.GetRequiredService<ServerChatClient>(),
            NullLogger<ChatUnreadState>.Instance));
    }

    [Fact]
    public async Task Chat_ShowsLauncherMessagesAsTheyArrive()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var threadId = await StartThreadAsync(adminId, friend.Id);

        await SendAsLauncherAsync(threadId, friend.Id, "Sent before the page opened");

        var cut = Render<Chat>();
        Open(cut, friend.UserName!);

        cut.WaitForAssertion(() => Assert.Contains("Sent before the page opened", cut.Markup), Timeout);

        await SendAsLauncherAsync(threadId, friend.Id, "Sent while the page is open");

        cut.WaitForAssertion(() => Assert.Contains("Sent while the page is open", cut.Markup), Timeout);
    }

    [Fact]
    public async Task Chat_MessageSentFromTheServerUi_IsStoredAsTheUserAndBroadcast()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var threadId = await StartThreadAsync(adminId, friend.Id);

        var broadcaster = Services.GetRequiredService<ChatBroadcaster>();
        var broadcast = new List<(Guid ThreadId, SDK.Models.ChatMessage Message, IReadOnlyCollection<Guid> Participants)>();
        void Record(Guid id, SDK.Models.ChatMessage message, IReadOnlyCollection<Guid> participants)
            => broadcast.Add((id, message, participants));

        broadcaster.MessageSent += Record;

        try
        {
            var cut = Render<Chat>();
            Open(cut, friend.UserName!);

            await cut.InvokeAsync(() => Services.GetRequiredService<ServerChatClient>().SendMessageAsync(threadId, "Sent from the browser"));

            cut.WaitForAssertion(() => Assert.Contains("Sent from the browser", cut.Markup), Timeout);

            var sent = Assert.Single(broadcast, b => b.ThreadId == threadId);
            Assert.Equal(adminId, sent.Message.UserId);
            Assert.Contains(friend.Id, sent.Participants);

            // Launchers page the thread through the same service, so it must be stored as the admin's
            var stored = await Services.GetRequiredService<ChatService>().GetMessagesAsync(threadId, 10);
            Assert.Equal(adminId, Assert.Single(stored.Items).UserId);
        }
        finally
        {
            broadcaster.MessageSent -= Record;
        }
    }

    [Fact]
    public async Task Chat_SwitchingThreads_ShowsOnlyTheSelectedThreadsMessages()
    {
        var adminId = await SignInAsync();
        var first = await AddUserAsync();
        var second = await AddUserAsync();

        await SendAsLauncherAsync(await StartThreadAsync(adminId, first.Id), first.Id, "In the first thread");
        await SendAsLauncherAsync(await StartThreadAsync(adminId, second.Id), second.Id, "In the second thread");

        var cut = Render<Chat>();

        Open(cut, first.UserName!);
        cut.WaitForAssertion(() => Assert.Contains("In the first thread", cut.Markup), Timeout);

        Open(cut, second.UserName!);
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("In the second thread", cut.Markup);
            Assert.DoesNotContain("In the first thread", cut.Markup);
        }, Timeout);
    }

    [Fact]
    public async Task Chat_OpensTheMostRecentThreadWithoutBeingAsked()
    {
        var adminId = await SignInAsync();
        var older = await AddUserAsync();
        var newer = await AddUserAsync();

        await SendAsLauncherAsync(await StartThreadAsync(adminId, older.Id), older.Id, "In the older thread");
        await SendAsLauncherAsync(await StartThreadAsync(adminId, newer.Id), newer.Id, "In the newer thread");

        var cut = Render<Chat>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("In the newer thread", cut.Markup);
            Assert.DoesNotContain("In the older thread", cut.Markup);
            Assert.Contains(newer.UserName!, cut.Find(".chat-list-thread-active").TextContent);
        }, Timeout);
    }

    [Fact]
    public async Task Chat_OpensTheThreadItWasAskedFor()
    {
        var adminId = await SignInAsync();
        var older = await AddUserAsync();
        var newer = await AddUserAsync();

        var olderThreadId = await StartThreadAsync(adminId, older.Id);

        await SendAsLauncherAsync(olderThreadId, older.Id, "In the older thread");
        await SendAsLauncherAsync(await StartThreadAsync(adminId, newer.Id), newer.Id, "In the newer thread");

        // As a clicked notification links to it
        var cut = Render<Chat>(parameters => parameters.Add(c => c.InitialThreadId, olderThreadId));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("In the older thread", cut.Markup);
            Assert.DoesNotContain("In the newer thread", cut.Markup);
        }, Timeout);

        Assert.Equal(olderThreadId, Services.GetRequiredService<ChatUnreadState>().OpenThreadId);
    }

    [Fact]
    public async Task ChatUnreadState_CountsMessagesUntilTheThreadIsRead()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var threadId = await StartThreadAsync(adminId, friend.Id);
        var state = Services.GetRequiredService<ChatUnreadState>();

        // The admin is shared with the other tests, whose threads may have unread messages of their own
        await state.RefreshAsync();
        var before = state.UnreadCount;

        await SendAsLauncherAsync(threadId, friend.Id, "First");
        await SendAsLauncherAsync(threadId, friend.Id, "Second");

        await state.RefreshAsync();
        Assert.Equal(before + 2, state.UnreadCount);

        // Opening the thread (the most recent) reads it
        var cut = Render<Chat>();

        cut.WaitForAssertion(() => Assert.Equal(before, state.UnreadCount), Timeout);
    }

    [Fact]
    public async Task ServerChatClient_RejectsUsersOutsideTheThread()
    {
        await SignInAsync();
        var first = await AddUserAsync();
        var second = await AddUserAsync();
        var threadId = await StartThreadAsync(first.Id, second.Id);

        var client = Services.GetRequiredService<ServerChatClient>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.SendMessageAsync(threadId, "Not my thread"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.GetMessagesAsync(threadId, null, 10));
    }

    [Fact]
    public async Task ServerChatClient_StartingAChatWithTheSamePeople_ReopensTheirThread()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var other = await AddUserAsync();
        var client = Services.GetRequiredService<ServerChatClient>();

        var first = await client.StartThreadAsync([friend.Id.ToString()]);
        var again = await client.StartThreadAsync([friend.Id.ToString(), adminId.ToString()]);
        var group = await client.StartThreadAsync([friend.Id.ToString(), other.Id.ToString()]);

        Assert.Equal(first, again);
        Assert.NotEqual(first, group);
        Assert.Single(await client.GetThreadsAsync(), t => t.Id == first);
    }

    [Fact]
    public async Task ServerChatClient_DeletedThread_ComesBackWithANewMessage()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var chatService = Services.GetRequiredService<ChatService>();
        var client = Services.GetRequiredService<ServerChatClient>();

        var threadId = await client.StartThreadAsync([friend.Id.ToString()]);

        await SendAsLauncherAsync(threadId, friend.Id, "Before it was deleted");
        await client.HideThreadAsync(threadId);

        Assert.DoesNotContain(await client.GetThreadsAsync(), t => t.Id == threadId);

        // Only gone for the one who deleted it
        Assert.Contains(await chatService.GetThreadsAsync(friend.Id), t => t.Id == threadId);

        var sent = await chatService.SendMessageAsync(threadId, friend.Id, "After it was deleted");

        // Senders re-announce the thread to these, so a launcher that dropped it picks it back up
        Assert.Equal([adminId], sent.RevealedTo);
        Assert.Contains(await client.GetThreadsAsync(), t => t.Id == threadId);

        // What was read before the delete stays read
        Assert.Equal(1, await client.GetUnreadMessageCountAsync(threadId));
    }

    [Fact]
    public async Task ServerChatClient_DeletedThread_ComesBackWhenStartedAgain()
    {
        await SignInAsync();
        var friend = await AddUserAsync();
        var client = Services.GetRequiredService<ServerChatClient>();

        var threadId = await client.StartThreadAsync([friend.Id.ToString()]);

        await client.HideThreadAsync(threadId);

        Assert.Equal(threadId, await client.StartThreadAsync([friend.Id.ToString()]));
        Assert.Contains(await client.GetThreadsAsync(), t => t.Id == threadId);
    }

    [Fact]
    public async Task Chat_RowDeleteButton_RemovesTheThreadOnceConfirmed()
    {
        var adminId = await SignInAsync();
        var kept = await AddUserAsync();
        var deleted = await AddUserAsync();

        var keptThreadId = await StartThreadAsync(adminId, kept.Id);
        var deletedThreadId = await StartThreadAsync(adminId, deleted.Id);

        await SendAsLauncherAsync(keptThreadId, kept.Id, "In the kept thread");
        await SendAsLauncherAsync(deletedThreadId, deleted.Id, "In the deleted thread");

        // Hosts the confirmation dialog
        var host = Render<ComponentHost>();
        var cut = Render<Chat>();

        Open(cut, kept.UserName!);

        cut.WaitForAssertion(() => Assert.Contains("In the kept thread", cut.Markup), Timeout);

        _ = cut.InvokeAsync(() => cut.FindAll(".chat-list-row")
            .Single(r => r.TextContent.Contains(deleted.UserName!))
            .QuerySelector(".chat-list-thread-delete")!
            .ClickAsync(new()));

        host.WaitForAssertion(() => Assert.NotEmpty(host.FindAll(".lc-dialog button")), Timeout);
        await host.InvokeAsync(() => host.FindAll(".lc-dialog button").First(b => b.TextContent.Trim() == "Delete").Click());

        cut.WaitForAssertion(() =>
        {
            Assert.DoesNotContain(cut.FindAll(".chat-list-thread"), t => t.TextContent.Contains(deleted.UserName!));

            // The open conversation wasn't the one deleted, so it stays open
            Assert.Contains("In the kept thread", cut.Markup);
        }, Timeout);

        Assert.DoesNotContain(await Services.GetRequiredService<ServerChatClient>().GetThreadsAsync(), t => t.Id == deletedThreadId);
    }

    [Fact]
    public async Task Chat_ShowsWhoIsTypingUntilTheirMessageArrives()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var threadId = await StartThreadAsync(adminId, friend.Id);
        var participants = new[] { adminId, friend.Id };
        var broadcaster = Services.GetRequiredService<ChatBroadcaster>();

        var cut = Render<Chat>();
        Open(cut, friend.UserName!);

        await broadcaster.TypingChangedAsync(threadId, friend.Id, true, participants);

        cut.WaitForAssertion(() => Assert.Contains($"{friend.UserName} is typing", cut.Find(".chat-thread-typing").TextContent), Timeout);

        await SendAsLauncherAsync(threadId, friend.Id, "Done typing");

        cut.WaitForAssertion(() => Assert.Equal(string.Empty, cut.Find(".chat-thread-typing").TextContent.Trim()), Timeout);

        // Nor is the viewer's own typing, from another tab or a launcher, shown back to them
        await broadcaster.TypingChangedAsync(threadId, adminId, true, participants);

        cut.WaitForAssertion(() => Assert.Contains("Done typing", cut.Markup), Timeout);
        Assert.Equal(string.Empty, cut.Find(".chat-thread-typing").TextContent.Trim());

        // The open thread marks the message read in the background; let it land, or the
        // shared admin's unread count shifts under whichever test runs next
        var chatService = Services.GetRequiredService<ChatService>();

        for (var waited = TimeSpan.Zero; await chatService.GetUnreadMessageCountAsync(threadId, adminId) > 0 && waited < Timeout; waited += TimeSpan.FromMilliseconds(50))
            await Task.Delay(50);
    }

    [Fact]
    public async Task ServerChatClient_SendTyping_ReachesTheThreadAsTheSignedInUser()
    {
        var adminId = await SignInAsync();
        var friend = await AddUserAsync();
        var threadId = await StartThreadAsync(adminId, friend.Id);

        var broadcaster = Services.GetRequiredService<ChatBroadcaster>();
        var heard = new List<(Guid ThreadId, Guid UserId, bool Typing)>();
        void Record(Guid id, Guid userId, bool typing) => heard.Add((id, userId, typing));

        broadcaster.TypingChanged += Record;

        try
        {
            var client = Services.GetRequiredService<ServerChatClient>();

            await client.SendTypingAsync(threadId, true);
            await client.SendTypingAsync(threadId, false);

            // Someone outside a thread can't announce themselves in it
            var elsewhere = await StartThreadAsync(friend.Id, (await AddUserAsync()).Id);
            await client.SendTypingAsync(elsewhere, true);

            Assert.Equal([(threadId, adminId, true), (threadId, adminId, false)], heard);
        }
        finally
        {
            broadcaster.TypingChanged -= Record;
        }
    }

    [Fact]
    public async Task ServerChatClient_CannotDeleteSomeoneElsesThread()
    {
        await SignInAsync();
        var first = await AddUserAsync();
        var second = await AddUserAsync();
        var threadId = await StartThreadAsync(first.Id, second.Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Services.GetRequiredService<ServerChatClient>().HideThreadAsync(threadId));
    }

    /// <summary>Gives the signed-in admin the user ID claim a real sign-in carries</summary>
    async Task<Guid> SignInAsync()
    {
        var admin = await Services.GetRequiredService<UserService>().GetAsync(TestConstants.AdminUserName);

        Authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()));

        return admin.Id;
    }

    async Task<User> AddUserAsync()
        => await Services.GetRequiredService<UserService>().AddAsync(new User { UserName = $"chat-{Guid.NewGuid():N}"[..16] });

    async Task<Guid> StartThreadAsync(params Guid[] participantIds)
    {
        var chatService = Services.GetRequiredService<ChatService>();
        var thread = await chatService.StartThreadAsync();

        foreach (var participantId in participantIds)
            await chatService.AddParticipantAsync(thread.Id, participantId);

        return thread.Id;
    }

    /// <summary>What the chat hub does with a launcher's message</summary>
    async Task SendAsLauncherAsync(Guid threadId, Guid senderId, string content)
    {
        var chatService = Services.GetRequiredService<ChatService>();

        var sent = await chatService.SendMessageAsync(threadId, senderId, content);

        await Services.GetRequiredService<ChatBroadcaster>().MessageSentAsync(
            threadId,
            Services.GetRequiredService<SdkMapper>().ToSdk(sent.Message),
            sent.ParticipantIds);
    }

    static void Open(IRenderedComponent<Chat> cut, string userName)
        => cut.WaitForAssertion(() =>
            cut.FindAll(".chat-list-thread").Single(thread => thread.TextContent.Contains(userName)).Click(), Timeout);
}
