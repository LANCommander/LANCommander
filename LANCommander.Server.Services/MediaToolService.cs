using LANCommander.SDK;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LANCommander.Server.Services;

public class MediaToolService(ILogger<MediaToolService> logger)
{
    private static readonly SemaphoreSlim _installLock = new(1, 1);

    public record ToolStatus(bool Installed, string? Path, string? Version);

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
                downloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp";
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
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task InstallFfmpegWindowsAsync(string toolsDir)
    {
        var downloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

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

            // Also copy ffprobe if available
            var ffprobeExe = Directory.GetFiles(extractDir, "ffprobe.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (ffprobeExe != null)
                File.Copy(ffprobeExe, Path.Combine(toolsDir, "ffprobe.exe"), true);
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
        logger.LogInformation("Installing ffmpeg via Homebrew");

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

        if (arch == Architecture.Arm64)
            downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linuxarm64-gpl.tar.xz";
        else
            downloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz";

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

            // Also copy ffprobe if available
            var ffprobeBin = Directory.GetFiles(toolsDir, "ffprobe", SearchOption.AllDirectories)
                .FirstOrDefault(f => !f.EndsWith(".tar.xz"));

            if (ffprobeBin != null)
            {
                var ffprobeDest = Path.Combine(toolsDir, "ffprobe");
                if (ffprobeBin != ffprobeDest)
                    File.Copy(ffprobeBin, ffprobeDest, true);

                await SetExecutableAsync(ffprobeDest);
            }
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
