using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Models;
using LANCommander.SDK;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly Services.MediaService MediaService;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public MediaController(
            ILogger<MediaController> logger,
            IMapper mapper,
            Services.MediaService mediaService) : base(logger)
        {
            Mapper = mapper;
            MediaService = mediaService;
        }

        [HttpGet]
        public async Task<IEnumerable<SDK.Models.Media>> Get()
        {
            return Mapper.Map<IEnumerable<SDK.Models.Media>>(await MediaService.Get());
        }

        [HttpGet("{id}")]
        public async Task<SDK.Models.Media> Get(Guid id)
        {
            return Mapper.Map<SDK.Models.Media>(await MediaService.Get(id));
        }

        [AllowAnonymous]
        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            try
            {
                var media = await MediaService.Get(id);

                var fs = System.IO.File.OpenRead(Services.MediaService.GetImagePath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }
    }
}
