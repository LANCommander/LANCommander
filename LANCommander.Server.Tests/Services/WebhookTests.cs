using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using LauncherEventReport = LANCommander.SDK.Models.LauncherEventReport;
using LauncherEventType = LANCommander.SDK.Models.LauncherEventType;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Events;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Rebus.Config;
using Rebus.Transport;
using Rebus.Transport.InMem;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public sealed class WebhookTests
{
    private const string Secret = "webhook-test-key-with-at-least-32-characters";

    [Fact]
    public async Task PublisherFiltersSubscriptionsAndPersistsBeforeTheBusStarts()
    {
        await using var test = await TestHost.CreateAsync();
        await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.PlayStarted));
        var disabled = Edit(ServerEventTypes.GameCreated);
        disabled.Enabled = false;
        await test.Webhooks.SaveAsync(null, disabled);
        var actorId = Guid.NewGuid();
        test.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "test")),
        };
        var gameId = Guid.NewGuid();

        await test.Publisher.PublishAsync(ServerEventTypes.GameCreated, "game", gameId, new { title = "Example" });

        await using var context = await test.Factory.CreateDbContextAsync();
        var delivery = (await context.Set<WebhookDelivery>().ToListAsync()).ShouldHaveSingleItem();
        using var body = JsonDocument.Parse(delivery.Body);
        body.RootElement.GetProperty("actorId").GetGuid().ShouldBe(actorId);
        body.RootElement.GetProperty("targetId").GetGuid().ShouldBe(gameId);
        body.RootElement.GetProperty("eventId").GetGuid().ShouldBe(delivery.EventId);
        body.RootElement.GetProperty("source").GetString().ShouldBe("LANCommander.Server");
        body.RootElement.GetProperty("occurredAt").GetDateTime().Kind.ShouldBe(DateTimeKind.Utc);
        body.RootElement.GetProperty("payload").GetProperty("title").GetString().ShouldBe("Example");
        delivery.Body.ShouldNotContain(Secret);
        delivery.Status.ShouldBe(WebhookDeliveryStatus.Pending);
    }

    [Fact]
    public async Task SecretsAreEncryptedWriteOnlyAndPreservedOnEdit()
    {
        await using var test = await TestHost.CreateAsync();
        var id = await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        string encrypted;
        await using (var context = await test.Factory.CreateDbContextAsync())
            encrypted = (await context.Set<WebhookSubscription>().FindAsync(id))!.ProtectedSecret;
        encrypted.ShouldNotContain(Secret);
        var edit = Edit(ServerEventTypes.PlayStarted);
        edit.Secret = null;
        await test.Webhooks.SaveAsync(id, edit);

        await using var updated = await test.Factory.CreateDbContextAsync();
        (await updated.Set<WebhookSubscription>().FindAsync(id))!.ProtectedSecret.ShouldBe(encrypted);
        JsonSerializer.Serialize(await test.Webhooks.GetAsync()).ShouldNotContain(Secret);
        JsonSerializer.Serialize(await test.Webhooks.GetAsync()).ShouldNotContain(encrypted);
    }

    [Fact]
    public async Task TestDeliveryUsesExactBodyHmacAndIsNotSentTwice()
    {
        await using var test = await TestHost.CreateAsync();
        var id = await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        var deliveryId = await test.Webhooks.TestAsync(id);

        await test.Handler.DeliverAsync(deliveryId);
        await test.Handler.DeliverAsync(deliveryId);

        var request = test.Receiver.Requests.ShouldHaveSingleItem();
        var expected = "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(request.Timestamp + "." + request.Body)));
        request.Signature.ShouldBe(expected);
        using var body = JsonDocument.Parse(request.Body);
        body.RootElement.GetProperty("eventType").GetString().ShouldBe(ServerEventTypes.Test);
        body.RootElement.GetProperty("eventId").GetGuid().ToString().ShouldBe(request.EventId);
        body.RootElement.GetProperty("targetId").GetGuid().ShouldBe(id);
        var delivery = (await test.Webhooks.GetDeliveriesAsync(id)).ShouldHaveSingleItem();
        delivery.Status.ShouldBe(WebhookDeliveryStatus.Delivered);
        delivery.Attempts.ShouldBe(1);
        delivery.StatusCode.ShouldBe(204);
    }

    [Theory]
    [InlineData(true, WebhookDeliveryStatus.Delivered)]
    [InlineData(false, WebhookDeliveryStatus.Failed)]
    public async Task FailedDeliveriesRetryWithBackoffAndStopAtTheAttemptLimit(bool recover, WebhookDeliveryStatus expected)
    {
        await using var test = await TestHost.CreateAsync();
        test.Receiver.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = Secret,
            Content = new StringContent(Secret),
        });
        var edit = Edit(ServerEventTypes.GameCreated);
        edit.MaxAttempts = 2;
        var id = await test.Webhooks.SaveAsync(null, edit);
        var deliveryId = await test.Webhooks.TestAsync(id);
        var before = DateTime.UtcNow;

        await test.Handler.DeliverAsync(deliveryId);

        var first = (await test.Webhooks.GetDeliveriesAsync(id)).ShouldHaveSingleItem();
        first.Status.ShouldBe(WebhookDeliveryStatus.Pending);
        first.NextAttemptOn.ShouldBeGreaterThanOrEqualTo(before.AddSeconds(5));
        first.Error.ShouldBe("HTTP 503");
        await test.Handler.DeliverAsync(deliveryId);
        test.Receiver.Requests.Count.ShouldBe(1);
        await test.MakeDueAsync(deliveryId);
        if (recover)
            test.Receiver.Response = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

        await test.Handler.DeliverAsync(deliveryId);
        await test.Handler.DeliverAsync(deliveryId);

        var final = (await test.Webhooks.GetDeliveriesAsync(id)).ShouldHaveSingleItem();
        final.Status.ShouldBe(expected);
        final.Attempts.ShouldBe(2);
        test.Receiver.Requests.Count.ShouldBe(2);
        JsonSerializer.Serialize(final).ShouldNotContain(Secret);
    }

    [Fact]
    public async Task SlowReceiversDoNotBlockPublishingAndDisabledSubscriptionsCancelPendingDelivery()
    {
        await using var test = await TestHost.CreateAsync();
        var id = await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        var deliveryId = await test.Webhooks.TestAsync(id);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Receiver.Response = async (_, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        };
        var sending = test.Handler.DeliverAsync(deliveryId);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await test.Publisher.PublishAsync(ServerEventTypes.GameCreated, "game", Guid.NewGuid(), new { })
                .WaitAsync(TimeSpan.FromSeconds(2));
            sending.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            release.TrySetResult();
            await sending;
        }
        var pendingId = (await test.Webhooks.GetDeliveriesAsync(id)).Single(d => d.Status == WebhookDeliveryStatus.Pending).Id;
        var disabled = Edit(ServerEventTypes.GameCreated);
        disabled.Enabled = false;
        disabled.Secret = null;
        await test.Webhooks.SaveAsync(id, disabled);

        await test.Handler.DeliverAsync(pendingId);

        (await test.Webhooks.GetDeliveriesAsync(id)).Single(d => d.Id == pendingId).Status.ShouldBe(WebhookDeliveryStatus.Canceled);
        test.Receiver.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PendingDeliveriesSurviveHostReplacementAndAreRecoveredByRebus()
    {
        var connectionString = $"Data Source=webhooks-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        await using var anchor = new SqliteConnection(connectionString);
        await anchor.OpenAsync();
        var protector = new EphemeralDataProtectionProvider();
        Guid subscriptionId;
        await using (var first = await TestHost.CreateAsync(connectionString, protector))
        {
            subscriptionId = await first.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
            await first.Publisher.PublishAsync(ServerEventTypes.GameCreated, "game", Guid.NewGuid(), new { title = "Durable" });
        }

        await using var restarted = await TestHost.CreateAsync(connectionString, protector);
        await restarted.Host.StartAsync();
        var deadline = DateTime.UtcNow.AddSeconds(15);
        WebhookDeliveryInformation delivery;
        do
        {
            delivery = (await restarted.Webhooks.GetDeliveriesAsync(subscriptionId)).ShouldHaveSingleItem();
            if (delivery.Status == WebhookDeliveryStatus.Delivered)
                break;
            await Task.Delay(50);
        } while (DateTime.UtcNow < deadline);

        delivery.Status.ShouldBe(WebhookDeliveryStatus.Delivered);
        restarted.Receiver.Requests.ShouldHaveSingleItem().Body.ShouldContain("Durable");
    }

    [Fact]
    public async Task PluginEventsAreMappedWithoutLeakingServerErrorsOrInitialStoppedStatus()
    {
        await using var test = await TestHost.CreateAsync();
        var edit = Edit(ServerEventTypes.GameCreated);
        edit.EventTypes = ServerEventTypes.All.ToArray();
        var id = await test.Webhooks.SaveAsync(null, edit);
        await test.Services.GetRequiredService<ServerLifecycleEvents>().StartAsync(CancellationToken.None);
        var bus = test.Services.GetRequiredService<IPluginEventBus>();
        var gameId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await bus.PublishAsync(new GameCreatedEvent(gameId, "A game"));
        await bus.PublishAsync(new ArchiveUploadedEvent(Guid.NewGuid(), gameId, Guid.NewGuid(), null, null, "1.0"));
        await bus.PublishAsync(new ArchiveUploadedEvent(Guid.NewGuid(), null, null, Guid.NewGuid(), null, "1.0"));
        await bus.PublishAsync(new PlaySessionStartedEvent(sessionId, gameId, userId, DateTime.UtcNow));
        await bus.PublishAsync(new PlaySessionEndedEvent(sessionId, gameId, userId, DateTime.UtcNow, DateTime.UtcNow, true));
        await bus.PublishAsync(new ServerStatusChangedEvent(serverId, "Dedicated", ServerEngineStatus.Stopped, null));
        await bus.PublishAsync(new ServerStatusChangedEvent(serverId, "Dedicated", ServerEngineStatus.Running, null));
        await bus.PublishAsync(new ServerStatusChangedEvent(serverId, "Dedicated", ServerEngineStatus.Running, null));
        await bus.PublishAsync(new ServerStatusChangedEvent(serverId, "Dedicated", ServerEngineStatus.Error, Secret));

        var deliveries = await test.Webhooks.GetDeliveriesAsync(id);
        deliveries.Select(d => d.EventType).ShouldBe(new[]
        {
            ServerEventTypes.GameCreated, ServerEventTypes.ArchiveUploaded, ServerEventTypes.PlayStarted,
            ServerEventTypes.PlayEnded, ServerEventTypes.ServerStarted, ServerEventTypes.ServerStopped,
        }, ignoreOrder: true);
        await using var context = await test.Factory.CreateDbContextAsync();
        (await context.Set<WebhookDelivery>().Select(d => d.Body).ToListAsync()).ShouldAllBe(body => !body.Contains(Secret));
    }

    [Fact]
    public async Task PluginTransportsAndConsumersReceiveEventsWithFailuresIsolated()
    {
        var transport = new RecordingTransport();
        var consumer = new RecordingConsumer();
        await using var test = await TestHost.CreateAsync(configure: services =>
        {
            services.AddSingleton<IServerEventTransport>(transport);
            services.AddSingleton<IServerEventConsumer, FailingConsumer>();
            services.AddSingleton<IServerEventConsumer>(consumer);
        });
        var subscriptionId = await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        await test.Host.StartAsync();
        var gameId = Guid.NewGuid();

        await test.Publisher.PublishAsync(ServerEventTypes.GameCreated, "game", gameId, new { title = "Plugin" });

        var received = await consumer.Received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        received.TargetId.ShouldBe(gameId);
        received.Payload.GetProperty("title").GetString().ShouldBe("Plugin");
        transport.QueueName.ShouldBe("server-events");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (test.Receiver.Requests.IsEmpty && DateTime.UtcNow < deadline)
            await Task.Delay(50);
        test.Receiver.Requests.ShouldHaveSingleItem().Body.ShouldContain("Plugin");
        (await test.Webhooks.GetDeliveriesAsync(subscriptionId)).ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentHandlersClaimEachAttemptOnce()
    {
        await using var test = await TestHost.CreateAsync();
        var id = await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        var deliveryId = await test.Webhooks.TestAsync(id);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        test.Receiver.Response = async (_, cancellationToken) =>
        {
            await release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        };

        var sending = Enumerable.Range(0, 8).Select(_ => Task.Run(() => test.Handler.DeliverAsync(deliveryId))).ToArray();
        await Task.Delay(500);
        release.TrySetResult();
        await Task.WhenAll(sending).WaitAsync(TimeSpan.FromSeconds(10));

        test.Receiver.Requests.Count.ShouldBe(1);
        var delivery = (await test.Webhooks.GetDeliveriesAsync(id)).ShouldHaveSingleItem();
        delivery.Status.ShouldBe(WebhookDeliveryStatus.Delivered);
        delivery.Attempts.ShouldBe(1);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("Player", HttpStatusCode.Forbidden)]
    [InlineData(SDK.Roles.Administrator, HttpStatusCode.OK)]
    public async Task WebhookApiRequiresAnAdministratorAndNeverReturnsTheSecret(string? role, HttpStatusCode expected)
    {
        await using var test = await TestHost.CreateAsync();
        await test.Webhooks.SaveAsync(null, Edit(ServerEventTypes.GameCreated));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(test.Webhooks);
        builder.Services.AddAuthorization();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", null);
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapWebhookEndpoints();
        await app.StartAsync();
        using var client = app.GetTestClient();
        if (role != null)
            client.DefaultRequestHeaders.Add("X-Test-Role", role);

        using var response = await client.GetAsync("/api/Webhooks");

        response.StatusCode.ShouldBe(expected);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldNotContain(Secret);
        body.ShouldNotContain("protectedSecret");
        if (expected != HttpStatusCode.OK)
        {
            using var write = await client.PostAsJsonAsync("/api/Webhooks", Edit(ServerEventTypes.GameCreated));
            write.StatusCode.ShouldBe(expected);
        }
    }

    [Fact]
    public async Task LauncherReportsAcceptOnlyKnownOutcomesAndUseTheAuthenticatedActor()
    {
        await using var test = await TestHost.CreateAsync();
        var edit = Edit(ServerEventTypes.GameInstalled);
        edit.EventTypes = ServerEventTypes.All.ToArray();
        var subscriptionId = await test.Webhooks.SaveAsync(null, edit);
        var actorId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var redistributableId = Guid.NewGuid();
        await using (var context = await test.Factory.CreateDbContextAsync())
        {
            context.Set<User>().Add(new User { Id = actorId, UserName = "player", Approved = true });
            context.Add(new Game { Id = gameId, Title = "Game" });
            context.Add(new Tool { Id = toolId, Name = "Tool" });
            context.Add(new Redistributable { Id = redistributableId, Name = "Redistributable" });
            await context.SaveChangesAsync();
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorId.ToString())], "test"));
        foreach (var type in Enum.GetValues<LauncherEventType>())
        {
            var targetId = type switch
            {
                LauncherEventType.ToolInstalled => toolId,
                LauncherEventType.RedistributableInstalled => redistributableId,
                _ => gameId,
            };
            var result = await EventEndpoints.ReportAsync(new LauncherEventReport { Type = type, TargetId = targetId },
                principal, test.Factory, test.Publisher);
            ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(202);
        }

        var invalid = await EventEndpoints.ReportAsync(new LauncherEventReport { Type = (LauncherEventType)999, TargetId = gameId },
            principal, test.Factory, test.Publisher);
        ((IStatusCodeHttpResult)invalid).StatusCode.ShouldBe(400);
        var missing = await EventEndpoints.ReportAsync(new LauncherEventReport { Type = LauncherEventType.GameInstalled, TargetId = Guid.NewGuid() },
            principal, test.Factory, test.Publisher);
        ((IStatusCodeHttpResult)missing).StatusCode.ShouldBe(404);
        var anonymous = await EventEndpoints.ReportAsync(new LauncherEventReport { Type = LauncherEventType.GameInstalled, TargetId = gameId },
            new ClaimsPrincipal(), test.Factory, test.Publisher);
        ((IStatusCodeHttpResult)anonymous).StatusCode.ShouldBe(401);
        (await test.Webhooks.GetDeliveriesAsync(subscriptionId)).Count.ShouldBe(5);
        await using var verify = await test.Factory.CreateDbContextAsync();
        foreach (var body in await verify.Set<WebhookDelivery>().Select(d => d.Body).ToListAsync())
        {
            using var json = JsonDocument.Parse(body);
            json.RootElement.GetProperty("actorId").GetGuid().ShouldBe(actorId);
            json.RootElement.GetProperty("payload").GetProperty("reportedByLauncher").GetBoolean().ShouldBeTrue();
            if (json.RootElement.GetProperty("eventType").GetString() == ServerEventTypes.GameInstallFailed)
                json.RootElement.GetProperty("status").GetString().ShouldBe("failed");
        }
    }

    [Fact]
    public async Task SQLiteMigrationCreatesUsableTablesAndCanBeRolledBack()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite(connection, options => options.MigrationsAssembly("LANCommander.Server.Data.SQLite")).Options);
        var migrations = context.GetService<IMigrationsAssembly>();
        var migrationType = migrations.Migrations.Single(m => m.Key.EndsWith("_AddWebhooks")).Value;
        var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);
        var generator = context.GetService<IMigrationsSqlGenerator>();
        foreach (var command in generator.Generate(migration.UpOperations, context.Model))
            await context.Database.ExecuteSqlRawAsync(command.CommandText);
        var subscription = new WebhookSubscription { Name = "Migration test" };
        context.Add(subscription);
        context.Add(new WebhookDelivery { Subscription = subscription, EventId = Guid.NewGuid(), EventType = ServerEventTypes.Test });
        await context.SaveChangesAsync();
        (await context.Set<WebhookDelivery>().CountAsync()).ShouldBe(1);

        foreach (var command in generator.Generate(migration.DownOperations, context.Model))
            await context.Database.ExecuteSqlRawAsync(command.CommandText);

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name IN ('WebhookDelivery', 'WebhookSubscription')";
        Convert.ToInt32(await query.ExecuteScalarAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("file:///C:/test", 10, 5)]
    [InlineData("https://user:password@example.com", 10, 5)]
    [InlineData("https://example.com?token=secret", 10, 5)]
    [InlineData("https://example.com", 0, 5)]
    [InlineData("https://example.com", 31, 5)]
    [InlineData("https://example.com", 10, 11)]
    public void InvalidSubscriptionsAreRejected(string endpoint, int timeout, int attempts)
    {
        var edit = Edit(ServerEventTypes.GameCreated);
        edit.Endpoint = endpoint;
        edit.TimeoutSeconds = timeout;
        edit.MaxAttempts = attempts;
        Should.Throw<ArgumentException>(() => WebhookService.Validate(edit, creating: true));
    }

    private static WebhookEdit Edit(string type) => new()
    {
        Name = "Example", Endpoint = "https://example.com/events", Secret = Secret, EventTypes = [type],
    };

    private sealed record ReceivedRequest(string Body, string Signature, string Timestamp, string EventId);

    private sealed class RecordingTransport : IServerEventTransport
    {
        private readonly InMemNetwork _network = new();
        public string? QueueName { get; private set; }

        public void ConfigureTransport(StandardConfigurer<ITransport> transport, string queueName)
        {
            QueueName = queueName;
            transport.UseInMemoryTransport(_network, queueName);
        }
    }

    private sealed class RecordingConsumer : IServerEventConsumer
    {
        public TaskCompletionSource<ServerEvent> Received { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(ServerEvent serverEvent, CancellationToken cancellationToken)
        {
            Received.TrySetResult(serverEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingConsumer : IServerEventConsumer
    {
        public Task HandleAsync(ServerEvent serverEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(Secret);
    }

    private sealed class Receiver : HttpMessageHandler
    {
        public ConcurrentQueue<ReceivedRequest> Requests { get; } = new();
        public Func<ReceivedRequest, CancellationToken, Task<HttpResponseMessage>> Response { get; set; } =
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var received = new ReceivedRequest(await request.Content!.ReadAsStringAsync(cancellationToken),
                request.Headers.GetValues("X-LANCommander-Signature").Single(),
                request.Headers.GetValues("X-LANCommander-Timestamp").Single(),
                request.Headers.GetValues("X-LANCommander-Event-Id").Single());
            Requests.Enqueue(received);
            return await Response(received, cancellationToken);
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role.ToString())], "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, "test")));
        }
    }

    private sealed class TestHost : IAsyncDisposable
    {
        public required IHost Host { get; init; }
        public required IServiceScope Scope { get; init; }
        public required SqliteConnection Anchor { get; init; }
        public required Receiver Receiver { get; init; }
        public IServiceProvider Services => Scope.ServiceProvider;
        public IDbContextFactory<DatabaseContext> Factory => Services.GetRequiredService<IDbContextFactory<DatabaseContext>>();
        public WebhookService Webhooks => Services.GetRequiredService<WebhookService>();
        public IServerEventPublisher Publisher => Services.GetRequiredService<IServerEventPublisher>();
        public WebhookDeliveryHandler Handler => ActivatorUtilities.CreateInstance<WebhookDeliveryHandler>(Services);

        public static async Task<TestHost> CreateAsync(string? connectionString = null, IDataProtectionProvider? protection = null,
            Action<IServiceCollection>? configure = null)
        {
            connectionString ??= $"Data Source=webhooks-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var anchor = new SqliteConnection(connectionString);
            await anchor.OpenAsync();
            var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContextFactory<DatabaseContext>(options => options.UseSqlite(connectionString));
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IPluginEventBus, PluginEventBus>();
            builder.Services.AddServerEvents();
            builder.Services.AddSingleton<IDataProtectionProvider>(protection ?? new EphemeralDataProtectionProvider());
            var receiver = new Receiver();
            builder.Services.AddHttpClient(WebhookDeliveryHandler.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => receiver);
            configure?.Invoke(builder.Services);
            var host = builder.Build();
            var result = new TestHost { Host = host, Scope = host.Services.CreateScope(), Anchor = anchor, Receiver = receiver };
            await using var context = await result.Factory.CreateDbContextAsync();
            await context.Database.EnsureCreatedAsync();
            return result;
        }

        public async Task MakeDueAsync(Guid id)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var delivery = await context.Set<WebhookDelivery>().FindAsync(id);
            delivery!.NextAttemptOn = DateTime.UtcNow.AddSeconds(-1);
            await context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Host.StopAsync();
            Scope.Dispose();
            Host.Dispose();
            await Anchor.DisposeAsync();
        }
    }
}
