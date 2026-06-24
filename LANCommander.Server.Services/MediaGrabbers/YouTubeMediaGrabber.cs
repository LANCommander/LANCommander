using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace LANCommander.Server.Services.MediaGrabbers
{
    public partial class YouTubeMediaGrabber(ILogger<YouTubeMediaGrabber> logger) : IMediaGrabberService
    {
        public string Name => "YouTube";

        public MediaType[] SupportedMediaTypes => [MediaType.Video];

        public async Task<IEnumerable<MediaGrabberResult>> SearchAsync(MediaType type, string keywords)
        {
            var youtube = new YoutubeClient();
            var results = new List<MediaGrabberResult>();
            var group = $"{keywords.Trim()}: Trailers & Gameplay";

            foreach (var video in (await youtube.Search.GetVideosAsync(keywords + " trailer gameplay")).Take(20))
            {
                var thumbnail = video.Thumbnails.GetWithHighestResolution();

                results.Add(new MediaGrabberResult()
                {
                    Id = video.Id,
                    Type = MediaType.Video,
                    SourceUrl = $"https://www.youtube.com/watch?v={video.Id}",
                    ThumbnailUrl = thumbnail?.Url ?? $"https://img.youtube.com/vi/{video.Id}/hqdefault.jpg",
                    Group = group,
                    MimeType = "video/mp4"
                });
            }

            return results;
        }

        public Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result)
            => DownloadAsync(result, null);

        public async Task<MediaGrabberDownload> DownloadAsync(MediaGrabberResult result, IProgress<MediaDownloadProgress>? progress)
        {
            if (!IsYouTubeUrl(result.SourceUrl))
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };

                using var response = await http.GetAsync(result.SourceUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                var stream = await ProgressStream.CopyToTempFileAsync(
                    await response.Content.ReadAsStreamAsync(), totalBytes, progress);

                return new MediaGrabberDownload
                {
                    Stream = stream,
                    MimeType = result.MimeType
                };
            }

            var ytdlpPath = await EnsureYtDlpAsync();

            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            var outputTemplate = Path.Combine(tempDir, "video.%(ext)s");

            progress?.Report(new MediaDownloadProgress { Status = "Starting yt-dlp..." });

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"--no-playlist --newline -f \"bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best\" --merge-output-format mp4 -o \"{outputTemplate}\" \"{result.SourceUrl}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            // Parse yt-dlp stdout for progress lines like "[download]  45.2% of 120.00MiB ..."
            var stdoutTask = Task.Run(async () =>
            {
                while (await process.StandardOutput.ReadLineAsync() is { } line)
                {
                    if (progress == null)
                        continue;

                    var match = YtDlpProgressRegex().Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                    {
                        progress.Report(new MediaDownloadProgress
                        {
                            BytesTransferred = (long)pct,
                            TotalBytes = 100,
                            Status = $"Downloading video... {pct:F1}%"
                        });
                    }
                    else if (line.Contains("[Merger]") || line.Contains("Merging"))
                    {
                        progress.Report(new MediaDownloadProgress
                        {
                            BytesTransferred = 100,
                            TotalBytes = 100,
                            Status = "Merging audio and video..."
                        });
                    }
                }
            });

            var stderrTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();
            await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                throw new Exception($"yt-dlp failed with exit code {process.ExitCode}: {stderr}");
            }

            var outputFile = Directory.GetFiles(tempDir).FirstOrDefault();
            if (outputFile == null)
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                throw new Exception("yt-dlp did not produce an output file");
            }

            var ext = Path.GetExtension(outputFile).TrimStart('.').ToLowerInvariant();
            var mimeType = ext switch
            {
                "mp4" => "video/mp4",
                "webm" => "video/webm",
                "mkv" => "video/x-matroska",
                _ => "video/mp4"
            };

            var stream2 = new TempFileStream(outputFile, tempDir);

            return new MediaGrabberDownload
            {
                Stream = stream2,
                MimeType = mimeType
            };
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"\[download\]\s+([\d.]+)%")]
        private static partial System.Text.RegularExpressions.Regex YtDlpProgressRegex();

        private static bool IsYouTubeUrl(string url) =>
            url.Contains("youtube.com/") || url.Contains("youtu.be/");

        private static readonly SemaphoreSlim _ytdlpLock = new(1, 1);

        private async Task<string> EnsureYtDlpAsync()
        {
            var isWindows = OperatingSystem.IsWindows();
            var fileName = isWindows ? "yt-dlp.exe" : "yt-dlp";

            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var dir in pathDirs)
            {
                var candidate = Path.Combine(dir, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            var toolsDir = Path.Combine(AppPaths.GetConfigDirectory(), "Tools");
            var localPath = Path.Combine(toolsDir, fileName);

            if (File.Exists(localPath))
                return localPath;

            await _ytdlpLock.WaitAsync();

            try
            {
                if (File.Exists(localPath))
                    return localPath;

                Directory.CreateDirectory(toolsDir);

                var downloadUrl = isWindows
                    ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
                    : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";

                logger.LogInformation("Downloading yt-dlp from {Url}", downloadUrl);

                using var http = new HttpClient();
                using var response = await http.GetAsync(downloadUrl);
                response.EnsureSuccessStatusCode();

                using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write))
                {
                    await response.Content.CopyToAsync(fs);
                }

                if (!isWindows)
                {
                    using var chmod = Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{localPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    if (chmod != null)
                        await chmod.WaitForExitAsync();
                }

                logger.LogInformation("yt-dlp downloaded to {Path}", localPath);

                return localPath;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to download yt-dlp");
                throw new Exception("yt-dlp is required to download YouTube videos but could not be found or downloaded. Install it manually or ensure internet access.", ex);
            }
            finally
            {
                _ytdlpLock.Release();
            }
        }

        private sealed class TempFileStream : FileStream
        {
            private readonly string _tempDir;

            public TempFileStream(string filePath, string tempDir)
                : base(filePath, FileMode.Open, FileAccess.Read)
            {
                _tempDir = tempDir;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);

                try
                {
                    if (Directory.Exists(_tempDir))
                        Directory.Delete(_tempDir, true);
                }
                catch { }
            }
        }
    }
}
