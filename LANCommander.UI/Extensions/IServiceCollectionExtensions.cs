using LANCommander.UI.Providers;
using LANCommander.UI.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.UI.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderUI(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider, LocalTimeProvider>();
        services.AddScoped<ScriptProvider>();
        services.AddScoped<UploadTracker>();

        return services;
    }
}