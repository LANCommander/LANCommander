using System.Net.Mime;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Importers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api;

//[Authorize(AuthenticationSchemes = "Bearer")]
[Route("api/[controller]")]
[ApiController]
public class ExportController : BaseApiController
{
    private readonly ImportService ImportService;
    
    public ExportController(
        ImportService importService,
        ILogger<ExportController> logger) : base(logger)
    {
        ImportService = importService;
    }

    //[Authorize(Roles = RoleService.AdministratorRoleName)]
    [HttpGet("{contextId:guid}")]
    public async Task ExportAsync(Guid contextId)
    {
        var context = ImportService.GetContext(contextId);
        
        var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        
        if (syncIOFeature != null)
            syncIOFeature.AllowSynchronousIO = true;
        
        Response.ContentType = MediaTypeNames.Application.Octet;
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"export.lcx\"");

        await ImportService.ExportAsync(contextId, Response.Body);
    }
}