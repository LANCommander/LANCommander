using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Settings");

        group.MapGet("/", GetAsync);
    }
    
    public static async Task<IResult> GetAsync(
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ServerService serverService,
        [FromServices] IMapper mapper)
    {
        var clientSettings = new
        {
            IPXRelay = new
            {
                Host = settingsProvider.CurrentValue.Server.IPXRelay.Host,
                Port = settingsProvider.CurrentValue.Server.IPXRelay.Port,
            },
            Library = new
            {
                EnableUserLibraries = settingsProvider.CurrentValue.Server.Library.EnableUserLibraries,
            }
        };

        return TypedResults.Ok(clientSettings);
    }
}