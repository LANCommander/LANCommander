using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Enums;
using LANCommander.Steam.Events;
using LANCommander.Steam.Models;
using LANCommander.Steam.Options;

namespace LANCommander.Steam.Services;

/// <summary>
/// Service for interacting with SteamCMD.
/// Can be constructed without dependency injection using optional parameters.
/// </summary>
public class SteamCmdService(
    SteamCmdOptions? options = null,
    ISteamCmdProfileStore? profileStore = null,
    ILogger<SteamCmdService>? logger = null)
    : ISteamCmdService
{
    private readonly ILogger<SteamCmdService>? _logger = logger;
    private readonly SteamCmdOptions _options = options ?? new SteamCmdOptions();
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private string? _executablePath;

    /// <summary>
    /// Event fired when an install job status changes
    /// </summary>
    public event EventHandler<SteamCmdInstallStatusEventArgs>? InstallStatusChanged;

    /// <summary>
    /// Event fired when install progress is updated
    /// </summary>
    public event EventHandler<SteamCmdInstallProgressEventArgs>? InstallProgress;

    /// <summary>
    /// Get or set the SteamCMD executable path
    /// </summary>
    public string? ExecutablePath
    {
        get => _executablePath ?? _options.ExecutablePath;
        set => _executablePath = value;
    }

    public async Task<SteamCmdConnectionStatus> GetConnectionStatusAsync(string username)
    {
        try
        {
            if (!IsValidUsername(username))
                throw new ArgumentException("Invalid username", nameof(username));
            
            // Auto-populate SteamCMD path if not configured
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                if (_options.AutoDetectPath)
                {
                    var detectedPath = await AutoDetectSteamCmdPathAsync();
                    
                    if (!string.IsNullOrWhiteSpace(detectedPath))
                    {
                        ExecutablePath = detectedPath;
                        _logger?.LogInformation("Auto-detected SteamCMD at: {Path}", detectedPath);
                    }
                    else
                    {
                        _logger?.LogWarning("SteamCMD path is not configured and could not be auto-detected");
                        return SteamCmdConnectionStatus.NotInstalled;
                    }
                }
                else
                {
                    _logger?.LogWarning("SteamCMD path is not configured and auto-detection is disabled");
                    return SteamCmdConnectionStatus.NotInstalled;
                }
            }

            if (!File.Exists(ExecutablePath))
            {
                _logger?.LogWarning("SteamCMD executable not found at configured path: {Path}", ExecutablePath);
                return SteamCmdConnectionStatus.NotInstalled;
            }

            // Try to run steamcmd with +quit to check if it's working
            var result = await ExecuteSteamCmdCommandAsync("+quit");
            
            if (result.Success)
            {
                // Check if we're logged in by trying to get user info
                var loginResult = await ExecuteSteamCmdCommandAsync($"+login {username} +quit", TimeSpan.FromSeconds(30));
                return loginResult.Success ? SteamCmdConnectionStatus.Authenticated : SteamCmdConnectionStatus.Unauthenticated;
            }

            return SteamCmdConnectionStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking SteamCMD connection status");
            return SteamCmdConnectionStatus.NotInstalled;
        }
    }

    public async Task<string> AutoDetectSteamCmdPathAsync()
    {
        var possiblePaths = new List<string>();

        // Windows paths
        if (OperatingSystem.IsWindows())
        {
            // Common Steam installation directories
            var steamPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam", "steamcmd.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Steam", "steamcmd.exe"),
                "C:\\Steam\\steamcmd.exe",
                "C:\\Program Files\\Steam\\steamcmd.exe",
                "C:\\Program Files (x86)\\Steam\\steamcmd.exe"
            };
            
            possiblePaths.AddRange(steamPaths);

            // Check PATH environment variable
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
            
            foreach (var dir in pathDirs)
            {
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    possiblePaths.Add(Path.Combine(dir, "steamcmd.exe"));
                }
            }
        }
        // Linux paths
        else if (OperatingSystem.IsLinux())
        {
            possiblePaths.AddRange(new[]
            {
                "/app/Data/Steam/steamcmd.sh",
                "/usr/local/bin/steamcmd",
                "/usr/bin/steamcmd",
                "/home/steam/steamcmd/steamcmd.sh",
                "/opt/steamcmd/steamcmd.sh",
                "/var/lib/steam/steamcmd/steamcmd.sh"
            });

            // Check PATH environment variable
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? [];
            
            foreach (var dir in pathDirs)
            {
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    possiblePaths.Add(Path.Combine(dir, "steamcmd"));
                }
            }
        }
        // macOS paths
        else if (OperatingSystem.IsMacOS())
        {
            possiblePaths.AddRange(new[]
            {
                "/usr/local/bin/steamcmd",
                "/opt/homebrew/bin/steamcmd",
                "/Applications/Steam.app/Contents/MacOS/steamcmd"
            });

            // Check PATH environment variable
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? [];
            
            foreach (var dir in pathDirs)
            {
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    possiblePaths.Add(Path.Combine(dir, "steamcmd"));
                }
            }
        }

        // Check each possible path
        foreach (var path in possiblePaths)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    // Verify it's actually SteamCMD by checking if it responds to --version or +quit
                    var testResult = await ExecuteSteamCmdCommandAsync("+quit", TimeSpan.FromSeconds(30), path);
                    
                    if (testResult.Success)
                    {
                        _logger?.LogDebug("Found SteamCMD at: {Path}", path);
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Failed to verify SteamCMD at: {Path}", path);
                }
            }
        }

        _logger?.LogWarning("SteamCMD not found in common installation locations");
        
        return string.Empty;
    }

    public async Task<SteamCmdStatus> LoginToSteamAsync(string username, string? password = null)
    {
        try
        {
            if (!IsValidUsername(username))
                throw new ArgumentException("Invalid username", nameof(username));
            
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                _logger?.LogError("SteamCMD path is not configured");
                return SteamCmdStatus.PathNotConfigured;
            }

            if (!File.Exists(ExecutablePath))
            {
                _logger?.LogError("SteamCMD executable not found at configured path: {Path}", ExecutablePath);
                return SteamCmdStatus.ExecutableNotFound;
            }

            var loginCommand = string.IsNullOrWhiteSpace(password) 
                ? $"+login {username}" 
                : $"+login {username} {password}";

            var result = await ExecuteSteamCmdCommandAsync($"{loginCommand} +quit", TimeSpan.FromSeconds(30));

            if (result.Output.Contains("Invalid Password"))
                return SteamCmdStatus.InvalidPassword;
            
            if (result.Success)
            {
                _logger?.LogInformation("Successfully logged into Steam as {Username}", username);
                return SteamCmdStatus.Success;
            }

            _logger?.LogError("Failed to log into Steam: {Error}", result.ErrorOutput);
            return SteamCmdStatus.Error;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error logging into Steam");
            return SteamCmdStatus.UnknownError;
        }
    }

    public async Task<SteamCmdStatus> LogoutAsync(string username)
    {
        try
        {
            if (!IsValidUsername(username))
                return SteamCmdStatus.InvalidUsername;
            
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                _logger?.LogError("SteamCMD path is not configured");
                return SteamCmdStatus.PathNotConfigured;
            }

            if (!File.Exists(ExecutablePath))
            {
                _logger?.LogError("SteamCMD executable not found at configured path: {Path}", ExecutablePath);
                return SteamCmdStatus.ExecutableNotFound;
            }

            var logoutCommand = $"+logout {username}";

            var result = await ExecuteSteamCmdCommandAsync($"{logoutCommand} +quit");
            
            if (result.Success)
            {
                _logger?.LogInformation("Successfully logged out of the Steam account {Username}", username);
                return SteamCmdStatus.Success;
            }

            _logger?.LogError("Failed to log out of Steam: {Error}", result.ErrorOutput);
            return SteamCmdStatus.Error;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error logging out of Steam");
            return SteamCmdStatus.UnknownError;
        }
    }
    
    public async Task InstallContentAsync(uint appId, string installDirectory, string? username = null)
    {
        if (!string.IsNullOrWhiteSpace(username) && !IsValidUsername(username))
            throw new ArgumentException("Invalid username", nameof(username));
        
        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            _logger?.LogError("SteamCMD path is not configured");
            throw new InvalidOperationException("SteamCMD path is not configured");
        }

        if (!File.Exists(ExecutablePath))
        {
            _logger?.LogError("SteamCMD executable not found at configured path: {Path}", ExecutablePath);
            throw new FileNotFoundException("SteamCMD executable not found", ExecutablePath);
        }
        
        Directory.CreateDirectory(installDirectory);

        var commands = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(username))
            commands.Add($"+login {username}");
        else
            commands.Add("+login anonymous");
        
        commands.Add($"+force_install_dir \"{installDirectory}\"");
        commands.Add($"+app_update {appId} validate");
        commands.Add("+quit");
        
        // Create install job
        var job = new SteamCmdInstallJob
        {
            AppId = appId,
            InstallDirectory = installDirectory,
            Username = username,
            Status = SteamCmdInstallStatus.InProgress,
            StatusMessage = "Starting installation...",
            CompletionSource = new TaskCompletionSource<SteamCmdStatus>()
        };
        
        OnInstallStatusChanged(job, SteamCmdInstallStatus.InProgress);

        try
        {
            var result =
                await ExecuteSteamCmdCommandWithProgressAsync(string.Join(" ", commands), job,
                    _cancellationTokenSource.Token);

            if (result.Success)
            {
                job.Status = SteamCmdInstallStatus.Completed;
                job.StatusMessage = "Installation completed sucessfully";
                job.Progress = 100;
                job.CompletedAt = DateTime.UtcNow;
                job.CompletionSource.TrySetResult(SteamCmdStatus.Success);

                OnInstallStatusChanged(job, SteamCmdInstallStatus.Completed);
                OnInstallProgress(job, 100, "Installation completed successfully");

                _logger?.LogInformation("Successfully installed Steam app {AppId} to {InstallDirectory}", job.AppId,
                    job.InstallDirectory);
            }
            else
            {
                job.Status = SteamCmdInstallStatus.Failed;
                job.StatusMessage = $"Installation failed: {result.ErrorOutput}";
                job.ErrorMessage = result.ErrorOutput;
                job.CompletedAt = DateTime.UtcNow;
                job.CompletionSource?.TrySetResult(SteamCmdStatus.Error);

                OnInstallStatusChanged(job, SteamCmdInstallStatus.Failed, result.ErrorOutput);

                _logger?.LogError("Failed to install Steam app {AppId}: {Error}", job.AppId, result.ErrorOutput);
            }
        }
        catch (Exception ex)
        {
            job.Status = SteamCmdInstallStatus.Failed;
            job.StatusMessage = $"Installation failed: {ex.Message}";
            job.ErrorMessage = ex.Message;
            job.CompletedAt = DateTime.UtcNow;
            job.CompletionSource?.TrySetException(ex);
            
            OnInstallStatusChanged(job, SteamCmdInstallStatus.Failed, ex.Message);
            
            _logger?.LogError(ex, "Error installing Steam content for app {AppId}", job.AppId);
        }
    }

    public async Task<SteamCmdStatus> RemoveContentAsync(string installDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                _logger?.LogError("Install directory is not specified");
                return SteamCmdStatus.InstallDirectoryNotFound;
            }

            if (!Directory.Exists(installDirectory))
            {
                _logger?.LogWarning("Install directory does not exist: {InstallDirectory}", installDirectory);
                return SteamCmdStatus.Success; // Consider it already removed
            }

            // Remove the directory and all its contents
            Directory.Delete(installDirectory, true);
            
            _logger?.LogInformation("Successfully removed content from {InstallDirectory}", installDirectory);
            return SteamCmdStatus.Success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing content from {InstallDirectory}", installDirectory);
            return SteamCmdStatus.UnknownError;
        }
    }

    public async Task<IEnumerable<SteamCmdProfile>> GetProfilesAsync()
    {
        if (profileStore == null)
        {
            throw new InvalidOperationException("Profile store is not configured. Provide an ISteamCmdProfileStore implementation.");
        }

        return await profileStore.GetAllAsync();
    }

    public async Task<SteamCmdProfile?> GetProfileAsync(string username)
    {
        if (profileStore == null)
        {
            throw new InvalidOperationException("Profile store is not configured. Provide an ISteamCmdProfileStore implementation.");
        }

        return await profileStore.GetByUsernameAsync(username);
    }

    public async Task SaveProfileAsync(SteamCmdProfile profile)
    {
        if (profileStore == null)
        {
            throw new InvalidOperationException("Profile store is not configured. Provide an ISteamCmdProfileStore implementation.");
        }

        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (string.IsNullOrWhiteSpace(profile.Username))
        {
            throw new ArgumentException("Profile username cannot be empty", nameof(profile));
        }

        await profileStore.SaveAsync(profile);
        _logger?.LogInformation("Saved profile for username: {Username}", profile.Username);
    }

    public async Task DeleteProfileAsync(string username)
    {
        if (profileStore == null)
        {
            throw new InvalidOperationException("Profile store is not configured. Provide an ISteamCmdProfileStore implementation.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }

        await profileStore.DeleteAsync(username);
        _logger?.LogInformation("Deleted profile for username: {Username}", username);
    }

    /// <inheritdoc />
    public async Task<SteamCmdAppInfo?> GetAppInfoAsync(uint appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            if (_options.AutoDetectPath)
            {
                var detected = await AutoDetectSteamCmdPathAsync();
                if (!string.IsNullOrEmpty(detected))
                    ExecutablePath = detected;
            }
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                _logger?.LogWarning("SteamCMD path is not configured; cannot get app changenumber.");
                return null;
            }
        }

        if (!File.Exists(ExecutablePath))
        {
            _logger?.LogWarning("SteamCMD executable not found at {Path}", ExecutablePath);
            return null;
        }

        // +quit can suppress output on some SteamCMD versions; we parse both stdout and stderr
        var arguments = $"+login anonymous +app_info_update 1 +app_info_print {appId} +quit";
        var result = await ExecuteSteamCmdCommandAsync(arguments, TimeSpan.FromSeconds(60));
        var combined = (result.Output + "\n" + result.ErrorOutput);

        var info = ParseAppInfoPrintOutput(appId, combined);
        if (info != null)
            _logger?.LogDebug("SteamCMD app info for {AppId}: changenumber={Changenumber}", appId, info.Changenumber);
        return info;
    }

    private static SteamCmdAppInfo? ParseAppInfoPrintOutput(uint appId, string output)
    {
        // VDF-like output: "buildid"		"3936003" and "timeupdated"		"1560990358" (often under "public" branch)
        var buildIdMatch = Regex.Match(output, @"""buildid""\s+""([^""]+)""", RegexOptions.IgnoreCase);
        var timeUpdatedMatch = Regex.Match(output, @"""timeupdated""\s+""([^""]+)""", RegexOptions.IgnoreCase);
        if (!buildIdMatch.Success)
            return null;

        var changenumber = buildIdMatch.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(changenumber))
            return null;

        DateTimeOffset? timeUpdated = null;
        if (timeUpdatedMatch.Success && long.TryParse(timeUpdatedMatch.Groups[1].Value.Trim(), out var unixSeconds))
            timeUpdated = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        return new SteamCmdAppInfo
        {
            AppId = appId,
            Changenumber = changenumber,
            TimeUpdated = timeUpdated
        };
    }

    private async Task<SteamCmdResult> ExecuteSteamCmdCommandWithProgressAsync(
        string arguments,
        SteamCmdInstallJob? job = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ExecutablePath))
            {
                throw new InvalidOperationException("SteamCMD executable path is not configured");
            }
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.Start();

            // Read streams with \r and \n as line delimiters so we get progress updates in real time.
            // SteamCMD uses \r for progress lines and full buffering when piped; BeginOutputReadLine
            // only fires on \n, so output would otherwise appear only at the end.
            var readStdOut = ReadStreamByLineAsync(
                process.StandardOutput.BaseStream,
                Encoding.UTF8,
                line =>
                {
                    if (string.IsNullOrEmpty(line))
                        return;
                    output.AppendLine(line);
                    _logger?.LogDebug("SteamCMD Output: {Output}", line);
                    if (job != null)
                        ParseProgressFromOutput(line, job);
                },
                cancellationToken);

            var readStdErr = ReadStreamByLineAsync(
                process.StandardError.BaseStream,
                Encoding.UTF8,
                line =>
                {
                    if (string.IsNullOrEmpty(line))
                        return;
                    error.AppendLine(line);
                    _logger?.LogDebug("SteamCMD Error: {Error}", line);
                    if (job != null)
                        ParseProgressFromOutput(line, job);
                },
                cancellationToken);

            try
            {
                await Task.WhenAll(
                    readStdOut,
                    readStdErr,
                    process.WaitForExitAsync(cancellationToken));
            }
            catch (OperationCanceledException)
            {
                _logger?.LogWarning("SteamCMD operation was cancelled");
                process.Kill();
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while executing SteamCMD");
            }

            return new SteamCmdResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                ErrorOutput = error.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing SteamCMD command: {Arguments}", arguments);
            
            return new SteamCmdResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput = ex.Message
            };
        }
    }

    /// <summary>
    /// Reads a stream and invokes the callback for each line. Lines are split on \r and \n
    /// so that progress output (often \r-only) is delivered in real time instead of buffered.
    /// </summary>
    private static async Task ReadStreamByLineAsync(
        Stream stream,
        Encoding encoding,
        Action<string> onLine,
        CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var lineBuilder = new StringBuilder();
        var charBuffer = new char[256];
        int read;
        while ((read = await reader.ReadAsync(charBuffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var ch = charBuffer[i];
                if (ch == '\r' || ch == '\n')
                {
                    if (lineBuilder.Length > 0)
                    {
                        onLine(lineBuilder.ToString());
                        lineBuilder.Clear();
                    }
                }
                else
                {
                    lineBuilder.Append(ch);
                }
            }
        }
        if (lineBuilder.Length > 0)
            onLine(lineBuilder.ToString());
    }

    private void ParseProgressFromOutput(string line, SteamCmdInstallJob job)
    {
        // SteamCMD progress patterns:
        // "Downloading update (X of Y MB)"
        // "Update state (X% at Y MB/s) : downloading update"
        // "Success. App 'X' fully installed."
        // "Installing update..."

        var progressMatch = Regex.Match(line, @"Update state \(([\d.]+)%", RegexOptions.IgnoreCase);
        if (progressMatch.Success && double.TryParse(progressMatch.Groups[1].Value, out var progress))
        {
            job.Progress = Math.Min(100, Math.Max(0, progress));
            
            // Try to extract download speed
            var speedMatch = Regex.Match(line, @"at ([\d.]+) (MB|KB)/s", RegexOptions.IgnoreCase);
            if (speedMatch.Success && double.TryParse(speedMatch.Groups[1].Value, out var speed))
            {
                var unit = speedMatch.Groups[2].Value.ToUpper();
                job.BytesPerSecond = (long)(speed * (unit == "MB" ? 1048576 : 1024));
            }

            OnInstallProgress(job, job.Progress, line, job.BytesDownloaded, job.BytesTotal, job.BytesPerSecond);
        }

        // Extract download progress (X of Y MB)
        var downloadMatch = Regex.Match(line, @"Downloading update \(([\d.]+) of ([\d.]+) (MB|KB)\)", RegexOptions.IgnoreCase);
        if (downloadMatch.Success)
        {
            if (double.TryParse(downloadMatch.Groups[1].Value, out var downloaded) &&
                double.TryParse(downloadMatch.Groups[2].Value, out var total))
            {
                var unit = downloadMatch.Groups[3].Value.ToUpper();
                var multiplier = unit == "MB" ? 1048576 : 1024;
                
                job.BytesDownloaded = (long)(downloaded * multiplier);
                job.BytesTotal = (long)(total * multiplier);
                
                if (job.BytesTotal > 0)
                {
                    job.Progress = (job.BytesDownloaded * 100.0) / job.BytesTotal;
                }
                
                OnInstallProgress(job, job.Progress, line, job.BytesDownloaded, job.BytesTotal, job.BytesPerSecond);
            }
        }

        // Update status message
        if (!string.IsNullOrWhiteSpace(line) && !line.Contains("Update state"))
        {
            job.StatusMessage = line.Trim();
        }
    }

    private void OnInstallStatusChanged(SteamCmdInstallJob job, SteamCmdInstallStatus status, string? errorMessage = null)
    {
        try
        {
            InstallStatusChanged?.Invoke(this, new SteamCmdInstallStatusEventArgs(job, status, errorMessage));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error invoking InstallStatusChanged event");
        }
    }

    private void OnInstallProgress(SteamCmdInstallJob job, double progress, string statusMessage, long bytesDownloaded = 0, long bytesTotal = 0, long bytesPerSecond = 0)
    {
        try
        {
            InstallProgress?.Invoke(this, new SteamCmdInstallProgressEventArgs(job, progress, statusMessage, bytesDownloaded, bytesTotal, bytesPerSecond));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error invoking InstallProgress event");
        }
    }

    private async Task<SteamCmdResult> ExecuteSteamCmdCommandAsync(string arguments, TimeSpan? timeout = null, string? steamCmdPath = null)
    {
        try
        {
            var executablePath = string.IsNullOrWhiteSpace(steamCmdPath) ? ExecutablePath : steamCmdPath;
            
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                throw new InvalidOperationException("SteamCMD executable path is not configured");
            }
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = processStartInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    _logger?.LogDebug("SteamCMD Output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                    _logger?.LogDebug("SteamCMD Error: {Error}", e.Data);
                }
            };

            process.Start();
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                if (timeout.HasValue)
                    await process.WaitForExitAsync().WaitAsync(timeout.Value);
                else
                    await process.WaitForExitAsync();
            }
            catch (TimeoutException)
            {
                _logger?.LogWarning("SteamCMD timed out");

                process.Kill();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,  "Error while executing SteamCMD");
            }

            return new SteamCmdResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                ErrorOutput = error.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error executing SteamCMD command: {Arguments}", arguments);
            
            return new SteamCmdResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput = ex.Message
            };
        }
    }
    
    private static bool IsValidUsername(string value)
    {
        return !string.IsNullOrEmpty(value) &&
               Regex.IsMatch(value, @"^[a-zA-Z0-9_]+$");
    }

    private class SteamCmdResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
    }
}