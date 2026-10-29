using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Plugins.Data;

/// <summary>A plugin's request for the host to register one of its contexts.</summary>
public sealed record PluginDbContextRegistration(Type ContextType);

public static class PluginDataServiceCollectionExtensions
{
    /// <summary>
    /// Asks the host to register <typeparamref name="TContext"/>, scoped, along with an
    /// <c>IDbContextFactory&lt;TContext&gt;</c>. Call it from <c>IPlugin.ConfigureServices</c>.
    /// </summary>
    /// <remarks>
    /// The host does the registration itself once every plugin has configured its services, so that
    /// it controls the connection and can refuse the context until its migrations are checked and
    /// applied. The plugin needs a <c>DataId</c> and its migrations assemblies listed in
    /// <c>[LANCommanderPlugin]</c>.
    /// </remarks>
    public static IServiceCollection AddPluginDbContext<TContext>(this IServiceCollection services)
        where TContext : PluginDbContext
    {
        services.AddSingleton(new PluginDbContextRegistration(typeof(TContext)));

        return services;
    }
}
