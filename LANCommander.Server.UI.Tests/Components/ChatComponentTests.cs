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

        var message = await chatService.SendMessageAsync(threadId, senderId, content);

        await Services.GetRequiredService<ChatBroadcaster>().MessageSentAsync(
            threadId,
            Services.GetRequiredService<SdkMapper>().ToSdk(message),
            await chatService.GetParticipantIdsAsync(threadId));
    }

    static void Open(IRenderedComponent<Chat> cut, string userName)
        => cut.WaitForAssertion(() =>
            cut.FindAll(".chat-list-thread").Single(thread => thread.TextContent.Contains(userName)).Click(), Timeout);
}
