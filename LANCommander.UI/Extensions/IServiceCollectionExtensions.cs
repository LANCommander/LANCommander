using LANCommander.UI.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.UI.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderUI(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider, LocalTimeProvider>();

        return services;
    }
}