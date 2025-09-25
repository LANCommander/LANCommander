using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Helpers;
using Syncfusion.PdfToImageConverter;
using System.Net.Mime;
using AutoMapper;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.Server.Services.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;

namespace LANCommander.Server.Services
{
    public sealed class MediaService(
        ILogger<SDK.Services.MediaService> logger,
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        StorageLocationService storageLocationService) : BaseDatabaseService<Media>(logger, cache, mapper, httpContextAccessor, contextFactory)
    {
        public override async Task<Media> AddAsync(Media entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);
            
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(m => m.Game);
                await context.UpdateRelationshipAsync(m => m.Parent);
                await context.UpdateRelationshipAsync(m => m.StorageLocation);
            });
        }

        public override async Task<Media> UpdateAsync(Media entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);
            
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(m => m.Game);
                await context.UpdateRelationshipAsync(m => m.Parent);
                await context.UpdateRelationshipAsync(m => m.StorageLocation);
            });
        }
        

        public override async Task DeleteAsync(Media entity)
        {
            DeleteLocalMediaFile(entity);
            
            await cache.ExpireGameCacheAsync(entity.GameId);

            await base.DeleteAsync(entity);
        }

        public override async Task DeleteRangeAsync(IEnumerable<Media> entities)
        {
            DeleteLocalMediaFiles(entities);

            var gameIds = entities.Select(x => x.GameId).Distinct();
            var expirationTasks = gameIds.Select(gameId => cache.ExpireGameCacheAsync(gameId));
            await Task.WhenAll(expirationTasks);

            await base.DeleteRangeAsync(entities);
        }

        public static bool FileExists(Media entity)
        {
            var path = GetMediaPath(entity);

            return File.Exists(path);
        }

        public async Task<bool> FileExistsAsync(Guid id)
        {
            var path = await GetMediaPathAsync(id);

            return File.Exists(path);
        }

        public bool ThumbnailExists(Media entity)
        {
            var path = GetThumbnailPath(entity);

            return File.Exists(path);
        }

        public async Task<string> GetMediaPathAsync(Guid id)
        {
            var entity = await GetAsync(id);

            return GetMediaPath(entity);
        }

        public static string GetMediaPath(Media entity)
        {
            return Path.Combine(entity.StorageLocation.Path, entity.FileId.ToString());
        }

        public static string GetMediaPath(Media entity, StorageLocation storageLocation)
        {
            return Path.Combine(storageLocation.Path, entity.FileId.ToString());
        }

        public async Task<string> GetThumbnailPathAsync(Guid id)
        {
            var entity = await GetAsync(id);

            return GetThumbnailPath(entity);
        }

        public string GetThumbnailPath(Media media)
        {
            var config = _settings.Media.GetMediaTypeConfig(media.Type);
            
            if (config != null && config.Thumbnails.Enabled)
                return $"{GetMediaPath(media)}.Thumb";
            else
                return GetMediaPath(media);
        }

        /*public async Task<Media> UploadMediaAsync(Stream stream, Media media)
        {
            return await UploadMediaAsync(stream, media);
            // return await UploadMediaAsync(file.OpenReadStream(maxAllowedSize: Settings.Media.MaxSize * 1024 * 1024), media);
        }*/

        public async Task<Media> WriteToFileAsync(Media media, Stream stream, bool overwrite = false)
        {
            if (media.StorageLocation == null)
                media.StorageLocation = await storageLocationService.GetAsync(media.StorageLocationId);
            
            if (media.StorageLocation == null)
                media.StorageLocation = await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
            
            if (media.FileId == Guid.Empty)
                media.FileId = Guid.NewGuid();
            
            var path = GetMediaPath(media);
            
            if (overwrite && File.Exists(path))
                File.Delete(path);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs);
            }

            media.Crc32 = await SDK.Services.MediaService.CalculateChecksumAsync(path);

            await GenerateThumbnailAsync(media);

            if (media.Id != Guid.Empty)
                media = await UpdateAsync(media);
            else
                media = await AddAsync(media);
            
            await cache.ExpireGameCacheAsync(media.Game?.Id ?? media.GameId);

            return media;
        }

        public async Task<string> GenerateThumbnailAsync(Media media, int quality = 75)
        {
            var source = GetMediaPath(media);
            var destination = GetThumbnailPath(media);

            if (!File.Exists(source))
                return String.Empty;

            var config = _settings.Media.GetMediaTypeConfig(media.Type);

            Stream stream = null;

            try
            {
                if (media.MimeType == MediaTypeNames.Application.Pdf)
                {
                    using (var pdfStream = new FileStream(source, FileMode.Open, FileAccess.Read))
                    {
                        var converter = new PdfToImageConverter();

                        converter.Load(pdfStream);

                        stream = converter.Convert(0, false, true);

                        stream.Seek(0, SeekOrigin.Begin);
                    }
                }
                else
                {
                    stream = new FileStream(source, FileMode.Open, FileAccess.Read);
                }

                if (config != null && config.Thumbnails.Enabled)
                {
                    using (var image = await Image.LoadAsync<Rgba32>(stream))
                    {
                        int thumbsizeX = (int)Math.Clamp(image.Width * (config.Thumbnails.Scale / 100f), config.Thumbnails.MinSize.Width, config.Thumbnails.MaxSize.Width);
                        int thumbsizeY = (int)Math.Clamp(image.Height * (config.Thumbnails.Scale / 100f), config.Thumbnails.MinSize.Height, config.Thumbnails.MaxSize.Height);
                        var resizeOptions = new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(thumbsizeX, thumbsizeY),
                            Sampler = KnownResamplers.Bicubic,
                        };

                        image.Mutate(context => context.Resize(resizeOptions));

                        if (media.Type.ValueIsIn(MediaType.Icon, MediaType.Logo, MediaType.PageImage) && media.MimeType == MediaTypeNames.Image.Png && HasTransparentPixels(image))
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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not generate thumbnail for media with ID {MediaId}", media.Id);
            }
            finally
            {
                if (stream is not null)
                    await stream.DisposeAsync();
            }

            return destination;
        }

        private bool HasTransparentPixels(Image<Rgba32> image)
        {
            var hasTransparentPixels = false;

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> pixelRow = accessor.GetRowSpan(y);

                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ref Rgba32 pixel = ref pixelRow[x];

                        if (pixel.A < 255)
                        {
                            hasTransparentPixels = true;
                            return;
                        }
                    }
                }
            });

            return hasTransparentPixels;
        }

        public void DeleteLocalMediaFile(Media media)
        {
            FileHelpers.DeleteIfExists(GetMediaPath(media));
            FileHelpers.DeleteIfExists(GetThumbnailPath(media));
        }

        public void DeleteLocalMediaFiles(IEnumerable<Media> medias)
        {
            foreach (var media in medias)
            {
                DeleteLocalMediaFile(media);
            }
        }

        public async Task<Media> DownloadMediaAsync(string sourceUrl, Media media)
        {
            using (var http = new HttpClient())
            {
                var response = await http.GetStreamAsync(sourceUrl);

                return await WriteToFileAsync(media, response);
            }
        }

        public async Task<StorageLocation> GetDefaultStorageLocationAsync()
        {
            var defaultStorageLocation = await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
            
            return defaultStorageLocation;
        }
    }
}
