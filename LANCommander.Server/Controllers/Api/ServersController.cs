using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer", Roles = RoleService.AdministratorRoleName)]
    [Route("api/[controller]")]
    [ApiController]
    public class ServersController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly ServerService ServerService;

        public ServersController(
            ILogger<ServersController> logger, 
            IMapper mapper,
            ServerService serverService,
            ArchiveService archiveService) : base(logger)
        {
            Mapper = mapper;
            ServerService = serverService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SDK.Models.Server>>> GetAsync()
        {
            return Ok(Mapper.Map<IEnumerable<SDK.Models.Server>>(await ServerService.GetAsync()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SDK.Models.Server>> Get(Guid id)
        {
            var server = await ServerService.GetAsync(id);

            if (server == null)
                return NotFound();

            return Ok(Mapper.Map<SDK.Models.Server>(server));
        }

        [HttpPost("Import/{objectKey}")]
        public async Task<IActionResult> ImportAsync(Guid objectKey)
        {
            try
            {
                var game = await ServerService.ImportAsync(objectKey);

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not import server from upload");
                return BadRequest(ex.Message);
            }
        }
    }
}
