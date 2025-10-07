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
        [FromServices] ServerService serverService,
        [FromServices] IMapper mapper)
    {
        var settings = SettingService.GetSettings();

        var clientSettings = new
        {
            IPXRelay = new
            {
                Host = settings.IPXRelay.Host,
                Port = settings.IPXRelay.Port,
            },
            Library = new
            {
                EnableUserLibraries = settings.Library.EnableUserLibraries,
            }
        };

        return TypedResults.Ok(clientSettings);
    }
}