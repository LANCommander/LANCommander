using Microsoft.AspNetCore.Routing;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Contributes HTTP endpoints to the server's API, for example for a launcher or another client
/// to call. Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// <para>
/// The server maps a plugin's endpoints under <c>/api/Plugins/{RoutePrefix}</c>, so they cannot
/// collide with the server's own routes or another plugin's. Clients authenticate the same way they
/// do for the rest of the API: a launcher's bearer token, or a signed-in browser session.
/// </para>
/// <para>
/// The server enforces <see cref="Access"/> on every endpoint the extension maps, before the
/// endpoint runs. As with pages, the default is administrators only, so a plugin that forgets to
/// declare access gets locked endpoints rather than open ones. A single endpoint can still be
/// narrowed with <c>RequireAuthorization</c>, or opened to anonymous callers with
/// <c>AllowAnonymous</c>, both visibly in the plugin's source.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class MyEndpoints : IServerEndpointExtension
/// {
///     public string RoutePrefix => "MyPlugin";
///     public PluginAccessPolicy Access => PluginAccessPolicy.AuthenticatedUser;
///
///     public void MapEndpoints(IEndpointRouteBuilder endpoints)
///     {
///         // GET /api/Plugins/MyPlugin/Status
///         endpoints.MapGet("/Status", (MyService service) => service.GetStatus());
///     }
/// }
/// </code>
/// </example>
public interface IServerEndpointExtension
{
    /// <summary>
    /// The path segment the endpoints are mapped under: <c>/api/Plugins/{RoutePrefix}</c>. Letters,
    /// digits, <c>.</c>, <c>-</c> and <c>_</c>, starting with a letter or digit. Must be unique among
    /// plugins, ignoring case; extensions that share a prefix are all ignored.
    /// </summary>
    string RoutePrefix { get; }

    /// <summary>Who may call the endpoints. Defaults to administrators only.</summary>
    PluginAccessPolicy Access => PluginAccessPolicy.Administrator;

    /// <summary>
    /// Maps the endpoints. <paramref name="endpoints"/> is already rooted at
    /// <c>/api/Plugins/{RoutePrefix}</c> and carries the <see cref="Access"/> requirement, so routes
    /// are given relative to it. Called once, when the server starts.
    /// </summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
