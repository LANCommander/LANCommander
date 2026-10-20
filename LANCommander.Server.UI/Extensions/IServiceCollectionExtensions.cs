using LANCommander.Server.UI.Providers;
using LANCommander.Server.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace LANCommander.Server.UI.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderServerUI(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider, LocalTimeProvider>();
        services.AddScoped<ScriptProvider>();
        services.AddScoped<UploadTracker>();

        services.AddRadzenComponents();

        services.AddScoped<LANCommander.Server.UI.Services.NotificationService>();
        services.AddScoped<LANCommander.Server.UI.Services.DialogService>();
        services.AddScoped<AlertService>();

        return services;
    }
}
