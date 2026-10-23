using System.Text.RegularExpressions;
using LANCommander.Server.Plugins;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// Maps the endpoints plugins contribute through <see cref="IServerEndpointExtension"/>, each under
/// its own <c>/api/Plugins/{RoutePrefix}</c> and behind the access policy it declares.
/// </summary>
/// <remarks>
/// The server applies the policy rather than trusting each plugin to, as it does for plugin pages:
/// an extension that declares nothing is reachable by administrators only.
/// </remarks>
public static partial class PluginEndpoints
{
    public const string Root = "/api/Plugins";

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    private static partial Regex ValidRoutePrefix();

    public static void MapPluginEndpoints(this IEndpointRouteBuilder routes)
    {
        var logger = routes.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(PluginEndpoints));

        foreach (var (extension, access) in Resolve(routes.ServiceProvider.GetServices<IServerEndpointExtension>(), logger))
        {
            var group = routes
                .MapGroup($"{Root}/{extension.RoutePrefix}")
                .RequireAuthorization(policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => access.IsSatisfiedBy(context.User)));

            try
            {
                extension.MapEndpoints(group);
            }
            catch (Exception ex)
            {
                // Whatever the extension mapped before it threw stays mapped, still behind its
                // policy; the server and every other plugin carry on.
                logger.LogError(ex, "Server endpoint extension {ExtensionType} failed to map its endpoints", extension.GetType().FullName);
            }
        }
    }

    /// <summary>
    /// The extensions that can be mapped, with the access each declares: invalid extensions, and
    /// every extension sharing a route prefix with another, are dropped with a warning.
    /// </summary>
    internal static IReadOnlyList<(IServerEndpointExtension Extension, PluginAccessPolicy Access)> Resolve(
        IEnumerable<IServerEndpointExtension> extensions,
        ILogger logger)
    {
        var valid = new List<(IServerEndpointExtension Extension, string Prefix, PluginAccessPolicy Access)>();

        foreach (var extension in extensions)
        {
            string? prefix;
            PluginAccessPolicy? access;

            try
            {
                prefix = extension.RoutePrefix;
                access = extension.Access;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Ignoring server endpoint extension {ExtensionType}, which could not be read", extension.GetType().FullName);
                continue;
            }

            if (prefix is null || !ValidRoutePrefix().IsMatch(prefix))
            {
                logger.LogWarning(
                    "Ignoring server endpoint extension {ExtensionType}: route prefix {RoutePrefix} is not a single path segment of letters, digits, '.', '-' and '_'",
                    extension.GetType().FullName,
                    prefix);
                continue;
            }

            // A missing policy must not widen access
            valid.Add((extension, prefix, access ?? PluginAccessPolicy.Administrator));
        }

        var duplicates = valid
            .GroupBy(entry => entry.Prefix, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in duplicates)
            logger.LogWarning("Ignoring server endpoint extensions with duplicate route prefix {RoutePrefix}", duplicate);

        return valid
            .Where(entry => !duplicates.Contains(entry.Prefix))
            .Select(entry => (entry.Extension, entry.Access))
            .ToList();
    }
}
