using AutoMapper;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace LANCommander.Server.Controllers
{
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class RedistributablesController : BaseController
    {
        private readonly IMapper Mapper;
        private readonly RedistributableService RedistributableService;
        private readonly ArchiveService ArchiveService;

        public RedistributablesController(
            ILogger<RedistributablesController> logger,
            IMapper mapper,
            RedistributableService redistributableService,
            ArchiveService archiveService) : base(logger)
        {
            Mapper = mapper;
            RedistributableService = redistributableService;
            ArchiveService = archiveService;
        }

        [HttpGet("/Redistributables/{id:guid}/Export")]
        public async Task Export(Guid id)
        {
            var redistributable = await RedistributableService.Get(id);

            if (redistributable == null)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.Append("Content-Disposition", @$"attachment; filename=""{redistributable.Name}.lcx""");

            using (ZipArchive export = new ZipArchive(Response.BodyWriter.AsStream(), ZipArchiveMode.Create))
            {
                var manifest = Mapper.Map<SDK.Models.Redistributable>(await RedistributableService.Get(redistributable.Id));

                var serializedManifest = ManifestHelper.Serialize(manifest);

                using (var manistream = new MemoryStream())
                using (var sw = new StreamWriter(manistream))
                {
                    sw.Write(serializedManifest);
                    sw.Flush();

                    var manifestEntry = export.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);

                    using (var entryStream = manifestEntry.Open())
                    {
                        manistream.Seek(0, SeekOrigin.Begin);
                        manistream.CopyTo(entryStream);
                    }
                }

                if (redistributable.Archives != null)
                foreach (var archive in redistributable.Archives)
                {
                    var archiveFilePath = ArchiveService.GetArchiveFileLocation(archive);
                    var entry = export.CreateEntry($"Archives/{archive.ObjectKey}", CompressionLevel.NoCompression);

                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(archiveFilePath))
                    {
                        await fileStream.CopyToAsync(entryStream);
                    }
                }

                if (redistributable.Scripts != null)
                foreach (var script in redistributable.Scripts)
                {
                    using (var scriptStream = new MemoryStream())
                    using (var sw = new StreamWriter(scriptStream))
                    {
                        sw.Write(script.Contents);
                        sw.Flush();

                        var scriptEntry = export.CreateEntry($"Scripts/{script.Id}", CompressionLevel.NoCompression);

                        using (var entryStream = scriptEntry.Open())
                        {
                            scriptStream.Seek(0, SeekOrigin.Begin);
                            scriptStream.CopyTo(entryStream);
                        }
                    }
                }
            }
        }
    }
}
