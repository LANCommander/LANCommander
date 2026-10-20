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
using SixLabors.ImageSharp.Formats.Webp;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.PE;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp.PixelFormats;
using LANCommander.SDK;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace LANCommander.Server.Services
{
    public sealed class MediaService(
        ILogger<MediaService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        StorageLocationService storageLocationService,
        MediaToolService mediaToolService) : BaseDatabaseService<Media>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public const long MaxIconSourceExecutableSize = 50 * 1024 * 1024;

        public override async Task<Media> AddAsync(Media entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            var media = await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(m => m.Game);
                await context.UpdateRelationshipAsync(m => m.Parent);
                await context.UpdateRelationshipAsync(m => m.StorageLocation);
            });

            await TouchGamesAsync(media.GameId ?? entity.GameId);

            return media;
        }

        public override async Task<Media> UpdateAsync(Media entity)
        {
            await cache.ExpireGameCacheAsync(entity.GameId);

            var media = await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(m => m.Game);
                await context.UpdateRelationshipAsync(m => m.Parent);
                await context.UpdateRelationshipAsync(m => m.StorageLocation);
            });

            await TouchGamesAsync(media.GameId ?? entity.GameId);

            return media;
        }

        public override async Task DeleteAsync(Media entity)
        {
            DeleteLocalMediaFile(entity);
            
            await cache.ExpireGameCacheAsync(entity.GameId);

            await base.DeleteAsync(entity);

            await TouchGamesAsync(entity.GameId);
        }

        public override async Task DeleteRangeAsync(IEnumerable<Media> entities)
        {
            var materialized = entities as IReadOnlyCollection<Media> ?? entities.ToList();

            DeleteLocalMediaFiles(materialized);

            var gameIds = materialized.Select(x => x.GameId).Distinct().ToArray();
            var expirationTasks = gameIds.Select(gameId => cache.ExpireGameCacheAsync(gameId));
            await Task.WhenAll(expirationTasks);

            await base.DeleteRangeAsync(materialized);

            await TouchGamesAsync(gameIds);
        }

        private async Task TouchGamesAsync(params Guid?[] gameIds)
        {
            var ids = gameIds
                .Where(id => id.HasValue && id.Value != Guid.Empty)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (ids.Count == 0)
                return;

            try
            {
                using var context = await contextFactory.CreateDbContextAsync();

                var games = await context.Set<Data.Models.Game>()
                    .Where(g => ids.Contains(g.Id))
                    .ToListAsync();

                if (games.Count == 0)
                    return;

                var timestamp = DateTime.UtcNow;

                foreach (var game in games)
                    game.UpdatedOn = timestamp;

                await context.SaveChangesAsync();

                foreach (var id in ids)
                    await cache.ExpireGameCacheAsync(id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not update the modified timestamp for game(s) {GameIds} after a media change", string.Join(", ", ids));
            }
        }

        public static bool FileExists(Media entity)
        {
            var path = GetMediaPath(entity);

            return File.Exists(path);
        }

        public bool ThumbnailExists(Media entity)
        {
            var path = GetThumbnailPath(entity);

            return File.Exists(path);
        }

        public async Task<string> GetMediaPathAsync(Guid id) =>
            GetMediaPath(await GetAsync(id));

        public static string GetMediaPath(Media entity) =>
            GetMediaPath(entity.FileId, entity.StorageLocation);

        public static string GetMediaPath(Guid id, StorageLocation storageLocation) =>
            AppPaths.ResolveStorageLocationPath(storageLocation.Path, id.ToString());

        public async Task<string> GetThumbnailPathAsync(Guid id)
        {
            var entity = await GetAsync(id);

            return GetThumbnailPath(entity);
        }

        public string GetThumbnailPath(Media media)
        {
            var config = _settingsProvider.CurrentValue.Server.Media.GetMediaTypeConfig(media.Type);
            
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
                media.StorageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Media);
            
            if (media.FileId == Guid.Empty)
                media.FileId = Guid.NewGuid();
            
            var path = GetMediaPath(media);
            
            if (!String.IsNullOrWhiteSpace(path))
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            
            if (overwrite && File.Exists(path))
                File.Delete(path);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs);
            }

            await ConvertAnimatedImageAsync(media, path);

            media.Crc32 = await SDK.Services.MediaClient.CalculateChecksumAsync(path);

            await GenerateThumbnailAsync(media);

            if (media.Id != Guid.Empty)
                media = await UpdateAsync(media);
            else
                media = await AddAsync(media);
            
            await cache.ExpireGameCacheAsync(media.Game?.Id ?? media.GameId);

            return media;
        }

        /// <summary>
        /// Extracts the primary icon out of a Windows executable and stores it as an ICO.
        /// </summary>
        /// <exception cref="InvalidDataException">
        /// The upload exceeds <see cref="MaxIconSourceExecutableSize"/>, is not a PE binary, or carries no
        /// icon resources.
        /// </exception>
        public async Task<Media> WriteIconFromExecutableAsync(Media media, Stream executable, CancellationToken cancellationToken = default)
        {
            using var extractor = await PEIconExtractor.FromStreamAsync(executable, MaxIconSourceExecutableSize, cancellationToken);

            var icon = extractor.ExtractPrimaryIcon()
                ?? throw new InvalidDataException("The executable does not contain any icons.");

            media.SourceUrl = String.Empty;
            media.MimeType = IconFile.MimeType;

            using var stream = new MemoryStream(icon, writable: false);

            return await WriteToFileAsync(media, stream);
        }

        public async Task<string> GenerateThumbnailAsync(Media media, int? quality = null)
        {
            var destination = GetThumbnailPath(media);
            var config = _settingsProvider.CurrentValue.Server.Media.GetMediaTypeConfig(media.Type);

            // The source is (re)written whenever this runs, so any sized variants cut from the old file are stale.
            DeleteSizedThumbnails(media);

            if (config == null || !config.Thumbnails.Enabled)
                return destination;

            await RenderThumbnailAsync(media, destination, quality ?? config.Thumbnails.Quality, (width, height) => new Size(
                (int)Math.Clamp(width * (config.Thumbnails.Scale / 100f), config.Thumbnails.MinSize.Width, config.Thumbnails.MaxSize.Width),
                (int)Math.Clamp(height * (config.Thumbnails.Scale / 100f), config.Thumbnails.MinSize.Height, config.Thumbnails.MaxSize.Height)));

            return destination;
        }

        /// <summary>
        /// Sizes a sized thumbnail request is rounded up to, so an anonymous caller can't make the server
        /// render and cache a variant for every pixel width.
        /// </summary>
        private static readonly int[] SizedThumbnailSteps = [64, 128, 192, 256, 384, 512, 768, 1024, 1280, 1536, 1920, 2560, 3840];

        private static readonly ConcurrentDictionary<string, Lazy<Task<bool>>> SizedThumbnailRenders = new();

        internal static int SnapThumbnailSize(int size)
        {
            if (size <= 0)
                return 0;

            foreach (var step in SizedThumbnailSteps)
            {
                if (step >= size)
                    return step;
            }

            return SizedThumbnailSteps[^1];
        }

        /// <summary>
        /// Path to a thumbnail fitting <paramref name="width"/> by <paramref name="height"/> pixels (either may be
        /// zero to leave that axis unconstrained), rendered from the original on first request and cached next to it.
        /// Never enlarges past the original. Falls back to the default thumbnail when no size is asked for, the media
        /// type has thumbnails turned off, or the variant can't be rendered.
        /// </summary>
        public async Task<string> GetThumbnailPathAsync(Media media, int width, int height)
        {
            width = SnapThumbnailSize(width);
            height = SnapThumbnailSize(height);

            var config = _settingsProvider.CurrentValue.Server.Media.GetMediaTypeConfig(media.Type);

            if ((width == 0 && height == 0)
                || config == null
                || !config.Thumbnails.Enabled
                || media.MimeType?.StartsWith("video/") == true)
                return GetThumbnailPath(media);

            var destination = GetSizedThumbnailPath(media, width, height);

            if (File.Exists(destination))
                return destination;

            // A freshly opened depot asks for every cover at once; render each variant once rather than per request.
            var render = SizedThumbnailRenders.GetOrAdd(destination, _ => new Lazy<Task<bool>>(() =>
                RenderThumbnailAsync(media, destination, config.Thumbnails.Quality, (sourceWidth, sourceHeight) =>
                {
                    var scale = 1d;

                    if (width > 0)
                        scale = Math.Min(scale, (double)width / sourceWidth);

                    if (height > 0)
                        scale = Math.Min(scale, (double)height / sourceHeight);

                    return new Size(
                        Math.Max(1, (int)Math.Round(sourceWidth * scale)),
                        Math.Max(1, (int)Math.Round(sourceHeight * scale)));
                })));

            try
            {
                return await render.Value ? destination : GetThumbnailPath(media);
            }
            finally
            {
                SizedThumbnailRenders.TryRemove(new KeyValuePair<string, Lazy<Task<bool>>>(destination, render));
            }
        }

        /// <summary>
        /// Ends in <c>.Thumb</c> so the regenerate-thumbnails tool clears variants and the orphaned-files scan skips them.
        /// </summary>
        private static string GetSizedThumbnailPath(Media media, int width, int height) =>
            $"{GetMediaPath(media)}.{(width > 0 ? $"w{width}" : "")}{(height > 0 ? $"h{height}" : "")}.Thumb";

        private static void DeleteSizedThumbnails(Media media)
        {
            var mediaPath = GetMediaPath(media);
            var directory = Path.GetDirectoryName(mediaPath);

            if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            foreach (var variant in Directory.EnumerateFiles(directory, $"{Path.GetFileName(mediaPath)}.*.Thumb"))
                FileHelpers.DeleteIfExists(variant);
        }

        /// <summary>
        /// Decodes the media's source (page one of a PDF, the largest frame of an ICO), resizes it into the box
        /// <paramref name="getSize"/> returns for the source dimensions, and writes it to <paramref name="destination"/>:
        /// PNG when transparency matters, JPEG otherwise.
        /// </summary>
        private async Task<bool> RenderThumbnailAsync(Media media, string destination, int quality, Func<int, int, Size> getSize)
        {
            var source = GetMediaPath(media);

            if (!File.Exists(source))
                return false;

            // Skip thumbnail generation for video types
            if (media.MimeType?.StartsWith("video/") == true)
                return false;

            Stream? stream = null;

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
                    stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
                }

                // ImageSharp has no ICO decoder, so icons are unpacked by our own PE/ICO reader.
                using (var image = IsIcon(media)
                           ? IconFile.DecodeLargestFrame(stream)
                           : await Image.LoadAsync<Rgba32>(stream))
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Mode = ResizeMode.Max,
                        Size = getSize(image.Width, image.Height),
                        Sampler = KnownResamplers.Bicubic,
                    };

                    image.Mutate(context => context.Resize(resizeOptions));

                    if (media.Type.ValueIsIn(MediaType.Icon, MediaType.Logo, MediaType.PageImage) && (media.MimeType == MediaTypeNames.Image.Png || media.MimeType == MediaTypeNames.Image.Webp || IsIcon(media)) && HasTransparentPixels(image))
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

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not generate thumbnail for media with ID {MediaId}", media.Id);

                // Don't leave a half-written file behind to be served as if it were complete.
                FileHelpers.DeleteIfExists(destination);

                return false;
            }
            finally
            {
                if (stream is not null)
                    await stream.DisposeAsync();
            }
        }

        /// <summary>Both the legacy and the IANA-registered MIME type turn up on ICO uploads.</summary>
        private static bool IsIcon(Media media) =>
            media.MimeType.ValueIsIn(IconFile.MimeType, "image/vnd.microsoft.icon");

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
            DeleteSizedThumbnails(media);
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

        private async Task ConvertAnimatedImageAsync(Media media, string path)
        {
            if (!string.Equals(media.MimeType, "image/apng", StringComparison.OrdinalIgnoreCase))
                return;

            var config = _settingsProvider.CurrentValue.Server.Media.GetMediaTypeConfig(media.Type);

            if (config == null || !config.AnimatedImage.ConvertToVideo)
                return;

            var ffmpegPath = mediaToolService.FindExecutable("ffmpeg");

            if (ffmpegPath == null)
            {
                _logger?.LogWarning("FFmpeg not found — skipping APNG to video conversion for media {MediaId}", media.Id);
                return;
            }

            var encoder = await mediaToolService.GetH264EncoderAsync(ffmpegPath);

            if (encoder == null)
            {
                _logger?.LogWarning(
                    "No supported H.264 encoder in the ffmpeg at {Path} — skipping APNG to video conversion for media {MediaId}",
                    ffmpegPath, media.Id);
                return;
            }

            var encoderArguments = MediaToolService.BuildH264Arguments(encoder, config.AnimatedImage.Quality);
            var tempOutput = path + ".mp4";

            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-y -i \"{path}\" {encoderArguments} -pix_fmt yuv420p -movflags +faststart -an \"{tempOutput}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();

                var stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    _logger?.LogError("FFmpeg APNG conversion failed (exit {ExitCode}): {Error}", process.ExitCode, stderr);

                    if (File.Exists(tempOutput))
                        File.Delete(tempOutput);

                    return;
                }

                File.Delete(path);
                File.Move(tempOutput, path);

                media.MimeType = "video/mp4";

                _logger?.LogInformation("Converted animated image to MP4 for media {MediaId}", media.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to convert APNG cover to video for media {MediaId}", media.Id);

                if (File.Exists(tempOutput))
                    File.Delete(tempOutput);
            }
        }

        public async Task<List<MediaOptimizationCandidate>> ScanOptimizationCandidatesAsync(MediaOptimizationOptions options)
        {
            var candidates = new List<MediaOptimizationCandidate>();
            var allMedia = await Include(m => m.StorageLocation, m => m.Game).GetAsync();

            foreach (var media in allMedia)
            {
                var path = GetMediaPath(media);

                if (!File.Exists(path))
                    continue;

                var mime = media.MimeType?.ToLowerInvariant();
                var isPng = mime == "image/png";
                var isJpeg = mime == "image/jpeg" || mime == "image/jpg";
                var isWebp = mime == "image/webp";

                if (!isPng && !isJpeg && !isWebp)
                    continue;

                int width;
                int height;

                try
                {
                    var info = await Image.IdentifyAsync(path);
                    width = info.Width;
                    height = info.Height;
                }
                catch
                {
                    continue;
                }

                var oversized = options.Downscale && Math.Max(width, height) > options.MaxLongEdge;
                var willConvert = isPng && options.ConvertPngToJpeg && media.Type != MediaType.Logo && media.Type != MediaType.Icon;
                var willRecompress = (isJpeg && options.RecompressJpeg) || (isWebp && options.RecompressWebp);

                if (!willConvert && !oversized && !willRecompress)
                    continue;

                var actions = new List<string>();

                if (willConvert)
                    actions.Add("PNG → JPEG");
                if (oversized)
                    actions.Add($"Downscale to {options.MaxLongEdge}px");
                if (willRecompress)
                    actions.Add("Recompress");
                if (options.StripMetadata)
                    actions.Add("Strip metadata");

                candidates.Add(new MediaOptimizationCandidate
                {
                    Id = media.Id,
                    GameTitle = media.Game?.Title ?? media.Name ?? media.Type.ToString(),
                    Type = media.Type,
                    MimeType = media.MimeType,
                    Width = width,
                    Height = height,
                    Size = new FileInfo(path).Length,
                    PlannedAction = string.Join(", ", actions),
                });
            }

            return candidates.OrderByDescending(c => c.Size).ToList();
        }

        public async Task<MediaOptimizationResult> OptimizeMediaAsync(Media media, MediaOptimizationOptions options)
        {
            var result = new MediaOptimizationResult();

            if (media.StorageLocation == null)
                media.StorageLocation = await storageLocationService.GetAsync(media.StorageLocationId);

            var path = GetMediaPath(media);

            if (!File.Exists(path))
                return result;

            var mime = media.MimeType?.ToLowerInvariant();
            var isPng = mime == "image/png";
            var isJpeg = mime == "image/jpeg" || mime == "image/jpg";
            var isWebp = mime == "image/webp";

            if (!isPng && !isJpeg && !isWebp)
                return result;

            result.BeforeBytes = new FileInfo(path).Length;

            try
            {
                using (var image = await Image.LoadAsync<Rgba32>(path))
                {
                    var changed = false;

                    if (options.Downscale && Math.Max(image.Width, image.Height) > options.MaxLongEdge)
                    {
                        image.Mutate(ctx => ctx.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new Size(options.MaxLongEdge, options.MaxLongEdge),
                            Sampler = KnownResamplers.Bicubic,
                        }));

                        changed = true;
                    }

                    var transparent = isPng && HasTransparentPixels(image);
                    var convertToJpeg = isPng && options.ConvertPngToJpeg && !transparent && media.Type != MediaType.Logo && media.Type != MediaType.Icon;

                    var hasMetadata = image.Metadata.ExifProfile != null
                        || image.Metadata.IccProfile != null
                        || image.Metadata.XmpProfile != null;

                    if (options.StripMetadata && hasMetadata)
                    {
                        image.Metadata.ExifProfile = null;
                        image.Metadata.IccProfile = null;
                        image.Metadata.XmpProfile = null;

                        changed = true;
                    }

                    var reencodeJpeg = isJpeg && (options.RecompressJpeg || changed);
                    var reencodeWebp = isWebp && (options.RecompressWebp || changed);

                    if (!convertToJpeg && !reencodeJpeg && !reencodeWebp && !(isPng && changed))
                        return result;

                    var tempPath = path + ".optimizing";

                    if (convertToJpeg || isJpeg)
                        await image.SaveAsJpegAsync(tempPath, new JpegEncoder { Quality = options.JpegQuality });
                    else if (isWebp)
                        await image.SaveAsWebpAsync(tempPath, new WebpEncoder { Quality = options.WebpQuality });
                    else
                        await image.SaveAsPngAsync(tempPath);

                    File.Delete(path);
                    File.Move(tempPath, path);

                    if (convertToJpeg)
                        media.MimeType = MediaTypeNames.Image.Jpeg;
                }

                media.Crc32 = await SDK.Services.MediaClient.CalculateChecksumAsync(path);

                await GenerateThumbnailAsync(media);

                await UpdateAsync(media);

                result.AfterBytes = new FileInfo(path).Length;
                result.Changed = true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not optimize media with ID {MediaId}", media.Id);
            }

            return result;
        }

        public async Task<StorageLocation> GetDefaultStorageLocationAsync()
        {
            var defaultStorageLocation = await storageLocationService.FirstOrDefaultAsync(l => l.Type == StorageLocationType.Media && l.Default);
            
            return defaultStorageLocation;
        }
    }
}
