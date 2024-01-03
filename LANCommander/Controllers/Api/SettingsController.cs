using AutoMapper;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class SettingsController : ControllerBase
    {
        private readonly IMapper Mapper;

        public SettingsController(IMapper mapper)
        {
            Mapper = mapper;
        }

        [HttpGet]
        public async Task<SDK.Models.Settings> Get()
        {
            return Mapper.Map<SDK.Models.Settings>(SettingService.GetSettings());
        }
    }
}
