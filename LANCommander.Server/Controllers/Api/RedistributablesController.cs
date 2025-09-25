using AutoMapper;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.ImportExport;
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
        private readonly ArchiveClient _archiveClient;
        private readonly ImportContext ImportContext;

        public RedistributablesController(
            ILogger<RedistributablesController> logger, 
            IMapper mapper,
            RedistributableService redistributableService,
            StorageLocationService storageLocationService,
            ArchiveClient archiveClient,
            ImportContext importContext) : base(logger)
        {
            Mapper = mapper;
            RedistributableService = redistributableService;
            StorageLocationService = storageLocationService;
            _archiveClient = archiveClient;
            ImportContext = importContext;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<SDK.Models.Redistributable>>> GetAsync()
        {
            return Ok(Mapper.Map<IEnumerable<SDK.Models.Redistributable>>(await RedistributableService.GetAsync()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SDK.Models.Redistributable>> GetAsync(Guid id)
        {
            var redistributable = await RedistributableService
                .Include(r => r.Archives)
                .Include(r => r.Scripts)
                .GetAsync(id);

            if (redistributable == null)
                return NotFound();

            return Ok(Mapper.Map<SDK.Models.Redistributable>(redistributable));
        }

        [HttpGet("{id}/Download")]
        public async Task<IActionResult> DownloadAsync(Guid id)
        {
            var redistributable = await RedistributableService
                .Include(r => r.Archives)
                .GetAsync(id);

            if (redistributable == null)
                return NotFound();

            if (redistributable.Archives == null || redistributable.Archives.Count == 0)
                return NotFound();

            var archive = redistributable.Archives.OrderByDescending(a => a.CreatedOn).First();

            var filename = await _archiveClient.GetArchiveFileLocationAsync(archive);

            if (!System.IO.File.Exists(filename))
                return NotFound();

            return File(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{redistributable.Name.SanitizeFilename()}.zip");
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("Import/{objectKey}")]
        public async Task<IActionResult> ImportAsync(Guid objectKey)
        {
            try
            {
                var uploadedPath = await _archiveClient.GetArchiveFileLocationAsync(objectKey.ToString());

                var result = await ImportContext.InitializeImportAsync(uploadedPath);

                return Ok(result);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not import redistributable from upload");
                return BadRequest(ex.Message);
            }
        }

        [Authorize(Roles = RoleService.AdministratorRoleName)]
        [HttpPost("UploadArchive")]
        public async Task<IActionResult> UploadArchiveAsync(SDK.Models.UploadArchiveRequest request)
        {
            try
            {
                var storageLocation = await StorageLocationService.FirstOrDefaultAsync(l => request.StorageLocationId.HasValue ? l.Id == request.StorageLocationId.Value : l.Default);
                var archive = await _archiveClient.FirstOrDefaultAsync(a => a.RedistributableId == request.Id && a.Version == request.Version);
                var archivePath = await _archiveClient.GetArchiveFileLocationAsync(archive);

                if (archive != null)
                {
                    System.IO.File.Delete(archivePath);

                    archive.ObjectKey = request.ObjectKey.ToString();
                    archive.Changelog = request.Changelog;
                    archive.CompressedSize = new System.IO.FileInfo(archivePath).Length;
                    archive.StorageLocation = storageLocation;

                    archive = await _archiveClient.UpdateAsync(archive);
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

                    await _archiveClient.AddAsync(archive);
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
