using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : BaseApiController
    {
        private readonly IMapper Mapper;

        public SettingsController(
            ILogger<SettingsController> logger,
            IMapper mapper) : base(logger)
        {
            Mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<SDK.Models.Settings>> GetAsync()
        {
            var settings = SettingService.GetSettings();

            return Ok(Mapper.Map<SDK.Models.Settings>(settings));
        }
    }
}
