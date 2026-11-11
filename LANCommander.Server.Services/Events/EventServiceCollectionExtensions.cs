using LANCommander.SDK.Events;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Rebus.Config;
using Rebus.Serialization.Json;

namespace LANCommander.Server.Services.Events;

public static class EventServiceCollectionExtensions
{
    internal const string BusName = "server-events";

    public static IServiceCollection AddServerEvents(this IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddScoped<IServerEventPublisher, ServerEventPublisher>();
        services.AddScoped<WebhookService>();
        services.AddHttpClient(WebhookDeliveryHandler.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                UseProxy = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            })
            .RemoveAllLoggers();

        // Resolved when the bus is created, so plugins can register a transport in
        // ConfigureServices regardless of ordering.
        var defaultTransport = new InMemoryServerEventTransport();
        services.AddRebus((configure, provider) =>
            {
                var transports = provider.GetServices<IServerEventTransport>().ToList();
                var transport = transports.FirstOrDefault() ?? defaultTransport;
                if (transports.Count > 1)
                    provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(EventServiceCollectionExtensions))
                        .LogWarning("{Count} server event transports are registered; using {Transport}",
                            transports.Count, transport.GetType().FullName);

                return configure
                    .Transport(t => transport.ConfigureTransport(t, BusName))
                    .Subscriptions(s => transport.ConfigureSubscriptions(s))
                    .Serialization(s => s.UseSystemTextJson())
                    .Options(o =>
                    {
                        o.SetNumberOfWorkers(1);
                        // Deliveries are claimed atomically, so a slow receiver or consumer can
                        // run alongside others.
                        o.SetMaxParallelism(4);
                    });
            },
            onCreated: bus => bus.Subscribe<ServerEvent>(),
            key: BusName);
        services.AddRebusHandler<ServerEventDispatcher>();
        services.AddRebusHandler<WebhookDeliveryHandler>();
        services.AddSingleton<IServerEventOutbox, WebhookEventOutbox>();
        services.AddSingleton<IServerEventConsumer, WebhookEventConsumer>();
        services.AddSingleton<WebhookDeliveryWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<WebhookDeliveryWorker>());
        services.AddSingleton<ServerLifecycleEvents>();
        services.AddHostedService(sp => sp.GetRequiredService<ServerLifecycleEvents>());
        return services;
    }
}