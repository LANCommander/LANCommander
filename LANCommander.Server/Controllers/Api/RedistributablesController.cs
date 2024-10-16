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
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class RedistributablesController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly RedistributableService RedistributableService;
        private readonly StorageLocationService StorageLocationService;
        private readonly ArchiveService ArchiveService;

        public RedistributablesController(
            ILogger<RedistributablesController> logger, 
            IMapper mapper,
            RedistributableService redistributableService,
            StorageLocationService storageLocationService,
            ArchiveService archiveService) : base(logger)
        {
            Mapper = mapper;
            RedistributableService = redistributableService;
            StorageLocationService = storageLocationService;
            ArchiveService = archiveService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SDK.Models.Redistributable>>> Get()
        {
            return Ok(Mapper.Map<IEnumerable<SDK.Models.Redistributable>>(await RedistributableService.Get()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SDK.Models.Redistributable>> Get(Guid id)
        {
            var redistributable = await RedistributableService.Get(id);

            if (redistributable == null)
                return NotFound();

            return Ok(Mapper.Map<SDK.Models.Redistributable>(redistributable));
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> Download(Guid id)
        {
            var redistributable = await RedistributableService.Get(id);

            if (redistributable == null)
                return NotFound();

            if (redistributable.Archives == null || redistributable.Archives.Count == 0)
                return NotFound();

            var archive = redistributable.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = ArchiveService.GetArchiveFileLocation(archive);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{redistributable.Name.SanitizeFilename()}.zip");
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("Import/{objectKey}")]
        public async Task<IActionResult> Import(Guid objectKey)
        {
            try
            {
                var game = await RedistributableService.Import(objectKey);

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not import redistributable from upload");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("UploadArchive")]
        public async Task<IActionResult> UploadArchive(SDK.Models.UploadArchiveRequest request)
        {
            try
            {
                var storageLocation = await StorageLocationService.FirstOrDefault(l => request.StorageLocationId.HasValue ? l.Id == request.StorageLocationId.Value : l.Default);
                var archive = await ArchiveService.FirstOrDefault(a => a.RedistributableId == request.Id && a.Version == request.Version);
                var archivePath = ArchiveService.GetArchiveFileLocation(archive);

                if (archive != null)
                {
                    System.IO.File.Delete(archivePath);

                    archive.ObjectKey = request.ObjectKey.ToString();
                    archive.Changelog = request.Changelog;
                    archive.CompressedSize = new System.IO.FileInfo(archivePath).Length;
                    archive.StorageLocation = storageLocation;

                    archive = await ArchiveService.Update(archive);
                }
                else
                {
                    archive = new Archive()
                    {
                        ObjectKey = request.ObjectKey.ToString(),
                        Changelog = request.Changelog,
                        RedistributableId = request.Id,
                        CompressedSize = new System.IO.FileInfo(archivePath).Length,
                        StorageLocation = storageLocation,
                    };

                    await ArchiveService.Add(archive);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not upload redistributable archive");
                return BadRequest(ex.Message);
            }
        }
    }
}
