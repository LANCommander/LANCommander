using LANCommander.SDK;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LANCommander.Server.Services;

public class MediaToolService(ILogger<MediaToolService> logger)
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);

    /// <summary>
    /// Preference order for H.264 encoding.
    /// </summary>
    /// <remarks>
    /// We cannot assume which ffmpeg build we are driving: <see cref="FindExecutable"/>
    /// checks PATH before our own Tools directory, so an operator's system ffmpeg wins
    /// when present. libx264 produces the best output but is GPL, so it is absent from
    /// the LGPL builds we install; it stays first here because invoking a binary the
    /// operator installed themselves is not us distributing it. libopenh264
    /// (BSD-2-Clause) is what our own LGPL builds carry.
    /// </remarks>
    private static readonly string[] H264EncoderPreference = ["libx264", "libopenh264"];

    /// <summary>Cached encoder probe results, keyed by ffmpeg path.</summary>
    private static readonly ConcurrentDictionary<string, string?> _h264EncoderCache = new();

    public record ToolStatus(bool Installed, string? Path, string? Version);

    /// <summary>
    /// Returns the best H.264 encoder the given ffmpeg build actually provides, or
    /// <c>null</c> if it has none.
    /// </summary>
    public async Task<string?> GetH264EncoderAsync(string ffmpegPath)
    {
        if (_h264EncoderCache.TryGetValue(ffmpegPath, out var cached))
            return cached;

        string? encoder = null;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = "-hide_banner -encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            encoder = H264EncoderPreference.FirstOrDefault(
                candidate => output.Contains(candidate, StringComparison.Ordinal));

            if (encoder == null)
                logger.LogWarning(
                    "ffmpeg at {Path} provides none of the supported H.264 encoders ({Encoders})",
                    ffmpegPath, string.Join(", ", H264EncoderPreference));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to probe ffmpeg encoders at {Path}", ffmpegPath);
        }

        _h264EncoderCache[ffmpegPath] = encoder;
        return encoder;
    }

    /// <summary>
    /// Builds the encoder and quality arguments for <paramref name="encoder"/>, given a
    /// quality value on x264's CRF scale (0-51, lower is better).
    /// </summary>
    public static string BuildH264Arguments(string encoder, int quality) => encoder switch
    {
        "libx264" => $"-c:v libx264 -crf {quality}",

        // openh264 has no CRF equivalent. It does honour a quantiser window, so centre
        // one on the configured CRF and the existing Quality setting keeps its meaning
        // and rough calibration. High profile rather than openh264's Constrained
        // Baseline default, matching x264 and cutting output size noticeably.
        "libopenh264" => $"-c:v libopenh264 -profile high -rc_mode quality "
                         + $"-qmin {Math.Clamp(quality - 5, 0, 51)} -qmax {Math.Clamp(quality + 5, 0, 51)}",

        // Unknown encoder: drive it at its own defaults rather than passing options it
        // may reject outright.
        _ => $"-c:v {encoder}",
    };

    public async Task<ToolStatus> GetYtDlpStatusAsync()
    {
        try
        {
            var path = FindExecutable("yt-dlp");

            if (path == null)
                return new ToolStatus(false, null, null);

            var version = await GetToolVersionAsync(path, "--version");

            return new ToolStatus(true, path, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check yt-dlp status");
            return new ToolStatus(false, null, null);
        }
    }

    public async Task<ToolStatus> GetFfmpegStatusAsync()
    {
        try
        {
            var path = FindExecutable("ffmpeg");

            if (path == null)
                return new ToolStatus(false, null, null);

            var version = await GetToolVersionAsync(path, "-version");

            return new ToolStatus(true, path, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check ffmpeg status");
            return new ToolStatus(false, null, null);
        }
    }

    public async Task InstallYtDlpAsync()
    {
        await _installLock.WaitAsync();

        try
        {
            var toolsDir = Path.Combine(AppPaths.GetConfigDirectory(), "Tools");
            Directory.CreateDirectory(toolsDir);

            var fileName = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";
            var localPath = Path.Combine(toolsDir, fileName);

            string downloadUrl;

            if (OperatingSystem.IsWindows())
            {
                downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
            }
            else if (OperatingSystem.IsMacOS())
            {
                downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos";
            }
            else
            {
                // Use the self-contained binary (bundles its own Python) instead of the
                // bare "yt-dlp" zipapp asset, which requires a system python3.
                var asset = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.Arm64 => "yt-dlp_linux_aarch64",
                    Architecture.Arm => "yt-dlp_linux_armv7l",
                    _ => "yt-dlp_linux",
                };

                downloadUrl = $"https://github.com/yt-dlp/yt-dlp/releases/latest/download/{asset}";
            }

            logger.LogInformation("Downloading yt-dlp from {Url}", downloadUrl);

            using var http = new HttpClient();
            using var response = await http.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            using (var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write))
            {
                await response.Content.CopyToAsync(fs);
            }

            if (!OperatingSystem.IsWindows())
                await SetExecutableAsync(localPath);

            logger.LogInformation("yt-dlp installed to {Path}", localPath);
        }
        finally
        {
            _installLock.Release();
        }
    }

    public async Task InstallFfmpegAsync()
    {
        await _installLock.WaitAsync();

        try
        {
            var toolsDir = Path.Combine(AppPaths.GetConfigDirectory(), "Tools");
            Directory.CreateDirectory(toolsDir);

            if (OperatingSystem.IsWindows())
            {
                await InstallFfmpegWindowsAsync(toolsDir);
            }
            else if (OperatingSystem.IsMacOS())
            {
                await InstallFfmpegViaBrew();
            }
            else
            {
                await InstallFfmpegLinuxAsync(toolsDir);
            }

            // The binary just changed, so any probed encoder list for it is stale.
            _h264EncoderCache.Clear();
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task InstallFfmpegWindowsAsync(string toolsDir)
    {
        // BtbN's "lgpl" variant rather than gyan.dev's "essentials" build, which is
        // configured with --enable-gpl. We auto-download this on the operator's behalf,
        // so it needs to be license-compatible with shipping alongside MIT-licensed
        // LANCommander. This build has no libx264; see GetH264EncoderAsync.
        var downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip";

        logger.LogInformation("Downloading ffmpeg from {Url}", downloadUrl);

        using var http = new HttpClient();
        using var response = await http.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();

        var zipPath = Path.Combine(toolsDir, "ffmpeg.zip");

        using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
        {
            await response.Content.CopyToAsync(fs);
        }

        var extractDir = Path.Combine(toolsDir, "ffmpeg-extract");

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

        // Find ffmpeg.exe in extracted directory
        var ffmpegExe = Directory.GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories).FirstOrDefault();

        if (ffmpegExe != null)
        {
            var destPath = Path.Combine(toolsDir, "ffmpeg.exe");
            File.Copy(ffmpegExe, destPath, true);
        }

        // Cleanup
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);

        logger.LogInformation("ffmpeg installed to {Path}", toolsDir);
    }

    private async Task InstallFfmpegViaBrew()
    {
        // Homebrew's ffmpeg is a GPL build, and there is no maintained LGPL prebuilt for
        // macOS to point at the way there is for Windows and Linux. This installs into
        // the operator's own Homebrew prefix rather than into our Tools directory, so we
        // are not redistributing it - but it is not the LGPL binary the other platforms
        // get, and libx264 will be present, so say so rather than let it pass silently.
        logger.LogWarning(
            "Installing ffmpeg via Homebrew. This is a GPL-licensed build, unlike the LGPL "
            + "builds installed on Windows and Linux. It is installed into your Homebrew "
            + "prefix and is not redistributed by LANCommander.");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "brew",
            Arguments = "install ffmpeg",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"Failed to install ffmpeg via Homebrew: {error}");
        }

        logger.LogInformation("ffmpeg installed via Homebrew");
    }

    private async Task InstallFfmpegLinuxAsync(string toolsDir)
    {
        var arch = RuntimeInformation.OSArchitecture;
        string downloadUrl;

        // "lgpl" rather than "gpl" variants: we auto-download these on the operator's
        // behalf, so they need to be license-compatible with shipping alongside
        // MIT-licensed LANCommander. Neither has libx264; see GetH264EncoderAsync.
        if (arch == Architecture.Arm64)
            downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linuxarm64-lgpl.tar.xz";
        else
            downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-lgpl.tar.xz";

        logger.LogInformation("Downloading ffmpeg from {Url}", downloadUrl);

        using var http = new HttpClient();
        using var response = await http.GetAsync(downloadUrl);
        response.EnsureSuccessStatusCode();

        var tarPath = Path.Combine(toolsDir, "ffmpeg.tar.xz");

        using (var fs = new FileStream(tarPath, FileMode.Create, FileAccess.Write))
        {
            await response.Content.CopyToAsync(fs);
        }

        // Extract using tar
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "tar",
            Arguments = $"-xf \"{tarPath}\" -C \"{toolsDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();
        await process.WaitForExitAsync();

        // Find ffmpeg binary in extracted directory
        var ffmpegBin = Directory.GetFiles(toolsDir, "ffmpeg", SearchOption.AllDirectories)
            .FirstOrDefault(f => !f.EndsWith(".tar.xz"));

        if (ffmpegBin != null)
        {
            var destPath = Path.Combine(toolsDir, "ffmpeg");
            if (ffmpegBin != destPath)
                File.Copy(ffmpegBin, destPath, true);

            await SetExecutableAsync(destPath);
        }

        // Cleanup
        if (File.Exists(tarPath))
            File.Delete(tarPath);

        // Remove extracted directories
        foreach (var dir in Directory.GetDirectories(toolsDir, "ffmpeg-*"))
            Directory.Delete(dir, true);

        logger.LogInformation("ffmpeg installed to {Path}", toolsDir);
    }

    public string? FindExecutable(string name)
    {
        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var fileName = isWindows ? $"{name}.exe" : name;

            // Check PATH
            var pathDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

            foreach (var dir in pathDirs)
            {
                try
                {
                    var candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch
                {
                    // Skip invalid PATH entries
                }
            }

            // Check local tools directory
            var toolsDir = Path.Combine(AppPaths.GetConfigDirectory(), "Tools");
            var localPath = Path.Combine(toolsDir, fileName);

            if (File.Exists(localPath))
                return localPath;

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to find executable {Name}", name);
            return null;
        }
    }

    private async Task<string?> GetToolVersionAsync(string path, string versionArg)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = versionArg,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var output = await process.StandardOutput.ReadLineAsync();
            await process.WaitForExitAsync();

            return output?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static async Task SetExecutableAsync(string path)
    {
        using var chmod = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"+x \"{path}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (chmod != null)
            await chmod.WaitForExitAsync();
    }
}
