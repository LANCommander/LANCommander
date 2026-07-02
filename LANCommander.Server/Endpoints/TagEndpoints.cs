using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class TagEndpoints
{
    public static void MapTagEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Tags");
        
        group.MapPost("/", CreateAsync)
            .RequireAuthorization("Administrator");
        
        group.MapPost("/{id:guid}", UpdateAsync)
            .RequireAuthorization("Administrator");
        
        group.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization("Administrator");
    }

    internal static async Task<IResult> CreateAsync(
        [FromBody] SDK.Models.Tag tag,
        [FromServices] TagService tagService,
        [FromServices] SdkMapper sdkMapper)
    {
        var entity = await tagService.AddAsync(sdkMapper.ToData(tag));
        
        return TypedResults.Ok(entity);
    }
    
    internal static async Task<IResult> UpdateAsync(
        Guid id,
        [FromBody] SDK.Models.Tag tag,
        [FromServices] TagService tagService,
        [FromServices] SdkMapper sdkMapper)
    {
        var existing = await tagService.GetAsync(id);
        
        if (existing == null)
            return TypedResults.NotFound();
        
        tag.Id = existing.Id;
        
        existing = await tagService.UpdateAsync(sdkMapper.ToData(tag));
        
        return TypedResults.Ok(existing);
    }
    
    internal static async Task<IResult> DeleteAsync(
        [FromBody] SDK.Models.Tag tag,
        [FromServices] TagService tagService)
    {
        var existing = await tagService.GetAsync(tag.Id);
        
        if (existing == null)
            return TypedResults.NotFound();

        await tagService.DeleteAsync(existing);
        
        return TypedResults.NoContent();
    }
}