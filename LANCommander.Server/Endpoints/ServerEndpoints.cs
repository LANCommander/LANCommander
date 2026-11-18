using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class ServerEndpoints
{
    public static void MapServerEndpoints(this IEndpointRouteBuilder routes)
    {
        // Everything here reveals how servers are run (paths, arguments, consoles) or controls them, so
        // it is for administrators only, as another LANCommander server linked to this one signs in as
        var group = routes.MapGroup("/api/Server").RequireAuthorization(RoleService.AdministratorRoleName);

        group.MapGet("/", GetAsync);
        group.MapGet("/Status", GetStatusesAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Status", GetStatusAsync);
        group.MapPost("/{id:guid}/Start", StartAsync);
        group.MapPost("/{id:guid}/Stop", StopAsync);
        group.MapGet("/{id:guid}/Log", GetLogAsync);
        group.MapGet("/{id:guid}/ProcessInfo", GetProcessInfoAsync);
        group.MapPost("/{id:guid}/Command", SendCommandAsync);
    }
    
    public static async Task<IResult> GetAsync(
        [FromServices] ServerService serverService,
        [FromServices] ServerManager serverManager,
        [FromServices] SdkMapper sdkMapper)
    {
        var servers = (await serverService.GetAsync()).Select(server => ToSdk(server, serverManager, sdkMapper)).ToList();

        return TypedResults.Ok(servers);
    }

    public static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] ServerService serverService,
        [FromServices] ServerManager serverManager,
        [FromServices] SdkMapper sdkMapper)
    {
        var server = await serverService.GetAsync(id);

        if (server == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(ToSdk(server, serverManager, sdkMapper));
    }

    /// <summary>The server as clients see it, at the address players use.</summary>
    static SDK.Models.Server ToSdk(Data.Models.Server server, ServerManager serverManager, SdkMapper sdkMapper)
    {
        var sdkServer = sdkMapper.ToSdk(server);
        var address = serverManager.GetAddress(server);

        sdkServer.Host = address.Host;
        sdkServer.Port = address.Port;

        return sdkServer;
    }

    /// <summary>
    /// The status of every server added here, by id, so another LANCommander server listing them needn't
    /// ask for each in turn.
    /// </summary>
    public static async Task<IResult> GetStatusesAsync(
        [FromServices] ServerService serverService,
        [FromServices] ServerManager serverManager)
    {
        var statuses = new Dictionary<Guid, Services.Enums.ServerProcessStatus>();

        foreach (var server in await serverService.AsNoTracking().GetAsync())
            statuses[server.Id] = await serverManager.GetStatusAsync(server.Id);

        return TypedResults.Ok(statuses);
    }

    public static async Task<IResult> GetStatusAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        return TypedResults.Ok(await serverManager.GetStatusAsync(id));
    }

    /// <summary>
    /// Starts a server, answering once it has begun to rather than when it stops: a local server's start
    /// lasts as long as its process. A start that fails within a few seconds is reported as a bad request.
    /// </summary>
    public static async Task<IResult> StartAsync(
        Guid id,
        [FromServices] ServerManager serverManager,
        [FromServices] ILogger<ServerManager> logger)
    {
        var start = Task.Run(() => serverManager.StartAsync(id));

        if (await Task.WhenAny(start, Task.Delay(TimeSpan.FromSeconds(5))) != start)
        {
            _ = start.ContinueWith(
                task => logger.LogError(task.Exception, "Server {ServerId} could not be started", id),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            return TypedResults.Ok();
        }

        try
        {
            await start;

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public static async Task<IResult> StopAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        try
        {
            await serverManager.StopAsync(id);

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// The server's buffered process output numbered after <paramref name="after"/>, at most
    /// <paramref name="tail"/> of the latest, so a linked LANCommander server can follow it.
    /// </summary>
    public static IResult GetLogAsync(
        Guid id,
        [FromServices] ServerManager serverManager,
        [FromQuery] long after = 0,
        [FromQuery] int tail = ServerManager.RecentLogCapacity)
    {
        return TypedResults.Ok(serverManager.GetRecentLogAfter(id, after, Math.Clamp(tail, 0, ServerManager.RecentLogCapacity)));
    }

    public static async Task<IResult> GetProcessInfoAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        return TypedResults.Ok(await serverManager.GetProcessInfoAsync(id));
    }

    public sealed record ServerCommandRequest(string Text);

    /// <summary>
    /// Sends a command as the server's Monitor page does: to the process's input where it takes it,
    /// otherwise over the server's first RCON console.
    /// </summary>
    public static async Task<IResult> SendCommandAsync(
        Guid id,
        [FromBody] ServerCommandRequest request,
        [FromServices] ServerService serverService,
        [FromServices] ServerManager serverManager,
        CancellationToken cancellationToken)
    {
        var server = await serverService
            .AsNoTracking()
            .Include(s => s.ServerConsoles)
            .GetAsync(id);

        if (server == null)
            return TypedResults.NotFound();

        Guid? consoleId = null;

        if (!await serverManager.CanSendCommandAsync(id))
        {
            consoleId = server.ServerConsoles?
                .FirstOrDefault(c => c.Type == SDK.Enums.ServerConsoleType.RCON && !String.IsNullOrWhiteSpace(c.Host) && c.Port > 0)?
                .Id;

            if (consoleId == null)
                return TypedResults.Ok(Services.Models.ServerCommandResult.Failed("The server isn't running with input to send commands to, and has no RCON console"));
        }

        return TypedResults.Ok(await serverManager.SendCommandAsync(id, consoleId, request.Text ?? "", cancellationToken));
    }
}
