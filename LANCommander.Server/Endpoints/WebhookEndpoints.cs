using LANCommander.Server.Services;
using LANCommander.Server.Services.Events;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.AspNetCore.Authorization;

namespace LANCommander.Server.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Webhooks")
            .RequireAuthorization(new AuthorizeAttribute { Roles = RoleService.AdministratorRoleName });
        group.MapGet("/", (WebhookService service) => service.GetAsync());
        group.MapGet("/EventTypes", () => ServerEventTypes.All);
        group.MapPost("/", (WebhookEdit edit, WebhookService service) => SaveAsync(null, edit, service));
        group.MapPut("/{id:guid}", (Guid id, WebhookEdit edit, WebhookService service) => SaveAsync(id, edit, service));
        group.MapDelete("/{id:guid}", async (Guid id, WebhookService service) =>
            await service.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
        group.MapGet("/{id:guid}/Deliveries", (Guid id, WebhookService service) => service.GetDeliveriesAsync(id));
        group.MapPost("/{id:guid}/Test", async (Guid id, WebhookService service) =>
        {
            try
            {
                return Results.Accepted(value: new { deliveryId = await service.TestAsync(id) });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }

    private static async Task<IResult> SaveAsync(Guid? id, WebhookEdit edit, WebhookService service)
    {
        try
        {
            return Results.Ok(new { id = await service.SaveAsync(id, edit) });
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
