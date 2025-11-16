using System.Net.Mime;
using LANCommander.Server.Services;
using LANCommander.Server.ImportExport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api;

//[Authorize(AuthenticationSchemes = "Bearer")]
[Route("api/[controller]")]
[ApiController]
public class ExportController : BaseApiController
{
    private readonly ExportService ExportService;
    
    public ExportController(
        ExportService exportService,
        ILogger<ExportController> logger,
        SettingsProvider<Settings.Settings> settingsProvider) : base(logger, settingsProvider)
    {
        ExportService = exportService;
    }

    //[Authorize(Roles = RoleService.AdministratorRoleName)]
    [HttpGet("{contextId:guid}")]
    public async Task ExportAsync(Guid contextId)
    {
        var context = ExportService.GetContext(contextId);
        
        var syncIOFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        
        if (syncIOFeature != null)
            syncIOFeature.AllowSynchronousIO = true;
        
        Response.ContentType = MediaTypeNames.Application.Octet;
        Response.Headers.Append("Content-Disposition", $"attachment; filename=\"export.lcx\"");

        await ExportService.ExportAsync(contextId, Response.Body);
    }
}