using AutoMapper;
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
        private readonly MediaService MediaService;

        public MediaController(
            ILogger<MediaController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            IMapper mapper,
            MediaService mediaService) : base(logger, settingsProvider)
        {
            Mapper = mapper;
            MediaService = mediaService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SDK.Models.Media>>> GetAsync()
        {
            return Ok(Mapper.Map<IEnumerable<SDK.Models.Media>>(await MediaService.GetAsync()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SDK.Models.Media>> GetAsync(Guid id)
        {
            var media = await MediaService.GetAsync(id);

            if (media == null)
                return NotFound();
            else
                return Mapper.Map<SDK.Models.Media>(media);
        }

        [AllowAnonymous]
        [HttpGet("{id}/Thumbnail")]
        public async Task<IActionResult> ThumbnailAsync(Guid id)
        {
            try
            {
                var media = await MediaService.GetAsync(id);

                var fs = System.IO.File.OpenRead(MediaService.GetThumbnailPath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [AllowAnonymous]
        [HttpGet("{id}/Download")]
        public async Task<IActionResult> DownloadAsync(Guid id)
        {
            try
            {
                var media = await MediaService.GetAsync(id);

                var fs = System.IO.File.OpenRead(Services.MediaService.GetMediaPath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }
    }
}
