using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using Syncfusion.PdfToImageConverter;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;

namespace LANCommander.Server.Services
{
    public class MediaService : BaseDatabaseService<Media>
    {
        private readonly StorageLocationService StorageLocationService;

        private Dictionary<MediaType, Size> ThumbnailSizes = new Dictionary<MediaType, Size>
        {
            { MediaType.Cover, new Size(600, 900) },
            { MediaType.Manual, new Size(600, 900) },
            { MediaType.Logo, new Size(640, 360) },
            { MediaType.Background, new Size(1920, 1080) },
            { MediaType.Icon, new Size(64, 64) },
            { MediaType.Avatar, new Size(128, 128) }
        };

        public MediaService(
            ILogger<MediaService> logger,
            IFusionCache cache,
            Repository<Media> repository,
            StorageLocationService storageLocationService) : base(logger, cache, repository)
        {
            StorageLocationService = storageLocationService;
        }

        public override Task Delete(Media entity)
        {
            DeleteLocalMediaFile(entity);

            return base.Delete(entity);
        }

        public bool FileExists(Media entity)
        {
            var path = GetMediaPath(entity);

            return File.Exists(path);
        }

        public async Task<bool> FileExists(Guid id)
        {
            var path = await GetMediaPath(id);

            return File.Exists(path);
        }

        public async Task<string> GetImagePath(Guid id)
        {
            var entity = await Get(id);

            return GetMediaPath(entity);
        }

        public static string GetMediaPath(Media entity)
        {
            return Path.Combine(entity.StorageLocation.Path, entity.FileId.ToString());
        }

        /*public async Task<Media> UploadMediaAsync(Stream stream, Media media)
        {
            return await UploadMediaAsync(stream, media);
            // return await UploadMediaAsync(file.OpenReadStream(maxAllowedSize: Settings.Media.MaxSize * 1024 * 1024), media);
        }*/

        public async Task<Media> UploadMediaAsync(Stream stream, Media media)
        {
            var fileId = Guid.NewGuid();
            var storageLocation = await StorageLocationService.FirstOrDefault(l => l.Type == StorageLocationType.Media && l.Default);

            media.FileId = fileId;
            media.StorageLocation = storageLocation;

            var path = GetMediaPath(media);

            media.Crc32 = SDK.Services.MediaService.CalculateChecksum(path);

            await GenerateThumbnailAsync(media);

            return media;
        }

        public async Task<string> GenerateThumbnailAsync(Media media, int quality = 75)
        {
            var destination = GetThumbnailPath(media);

            Stream stream = null;

            try
            {
                if (media.MimeType == MediaTypeNames.Application.Pdf)
                {
                    using (var pdfStream = new FileStream(GetMediaPath(media), FileMode.Open, FileAccess.Read))
                    {
                        var converter = new PdfToImageConverter();

                        converter.Load(stream);

                        stream = converter.Convert(0, false, true);

                        stream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else
                {
                    stream = new FileStream(GetMediaPath(media), FileMode.Open, FileAccess.Read);
                }

                if (ThumbnailSizes.ContainsKey(media.Type))
                {
                    using (var image = await Image.LoadAsync(stream))
                    {
                        var resizeOptions = new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = ThumbnailSizes[media.Type],
                            Sampler = KnownResamplers.Bicubic,
                        };

                        image.Mutate(context => context.Resize(resizeOptions));

                        if (media.Type.IsIn(MediaType.Icon, MediaType.Logo, MediaType.PageImage) && media.MimeType == MediaTypeNames.Image.Png)
                        {
                            await image.SaveAsPngAsync(destination);
                        }
                        else
                        {
                            await image.SaveAsJpegAsync(destination, new JpegEncoder
                            {
                                Quality = quality
                            });
                        }
                    }
                }
            }
            finally
            {
                if (stream is not null)
                    await stream.DisposeAsync();
            }

            return destination;
        }

        private string GetThumbnailPath(Media media)
        {
            if (ThumbnailSizes.ContainsKey(media.Type))
                return $"{GetMediaPath(media)}.Thumb";
            else
                return GetMediaPath(media);
        }

        public void DeleteLocalMediaFile(Media media)
        {
            FileHelpers.DeleteIfExists(GetMediaPath(media));
            FileHelpers.DeleteIfExists(GetThumbnailPath(media));
        }

        public async Task<Media> DownloadMediaAsync(string sourceUrl, Media media)
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetStreamAsync(sourceUrl);

                return await UploadMediaAsync(response, media);
            }
        }
    }
}
