using AutoMapper;
using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.Models;
using LANCommander.SDK;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class MediaController : ControllerBase
    {
        private readonly IMapper Mapper;
        private readonly MediaService MediaService;
        private readonly LANCommanderSettings Settings = SettingService.GetSettings();

        public MediaController(IMapper mapper, MediaService mediaService)
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

                var fs = System.IO.File.OpenRead(MediaService.GetImagePath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }
    }
}
