using LANCommander.Server.Services.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace LANCommander.Server.Services;

public class SteamCMDService(
    ILogger<SteamCMDService> logger,
    SettingsProvider<Settings.Settings> settingsProvider) : BaseService(logger, settingsProvider)
{
    public async Task<SteamCmdConnectionStatus> GetConnectionStatusAsync(string username)
    {
        try
        {
            // Auto-populate SteamCMD path if not configured
            if (string.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                var detectedPath = await AutoDetectSteamCmdPathAsync();
                
                if (!string.IsNullOrWhiteSpace(detectedPath))
                {
                    _settingsProvider.Update(s =>
                    {
                        s.Server.SteamCMD.Path = detectedPath;
                    });
                    
                    _logger.LogInformation("Auto-detected SteamCMD at: {Path}", detectedPath);
                }
                else
                {
                    _logger.LogWarning("SteamCMD path is not configured and could not be auto-detected");
                    return SteamCmdConnectionStatus.NotInstalled;
                }
            }

            if (!File.Exists(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogWarning("SteamCMD executable not found at configured path: {Path}", _settingsProvider.CurrentValue.Server.SteamCMD.Path);
                return SteamCmdConnectionStatus.NotInstalled;
            }

            // Try to run steamcmd with +quit to check if it's working
            var result = await ExecuteSteamCmdCommandAsync("+quit");
            
            if (result.Success)
            {
                // Check if we're logged in by trying to get user info
                #warning Susceptible to some sort of injection attack
                var loginResult = await ExecuteSteamCmdCommandAsync($"+login {username} +quit");
                return loginResult.Success ? SteamCmdConnectionStatus.Authenticated : SteamCmdConnectionStatus.Unauthenticated;
            }

            return SteamCmdConnectionStatus.NotInstalled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SteamCMD connection status");
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
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();
            
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
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? Array.Empty<string>();
            
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
            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(':') ?? Array.Empty<string>();
            
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
                    var testResult = await ExecuteSteamCmdCommandAsync("+quit", path);
                    
                    if (testResult.Success)
                    {
                        _logger.LogDebug("Found SteamCMD at: {Path}", path);
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to verify SteamCMD at: {Path}", path);
                }
            }
        }

        _logger.LogWarning("SteamCMD not found in common installation locations");
        
        return String.Empty;
    }

    public async Task<bool> LoginToSteamAsync(string username, string password = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD path is not configured");
                return false;
            }

            if (!File.Exists(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD executable not found at configured path: {Path}", _settingsProvider.CurrentValue.Server.SteamCMD.Path);
                return false;
            }

            var loginCommand = string.IsNullOrWhiteSpace(password) 
                ? $"+login {username}" 
                : $"+login {username} {password}";

            var result = await ExecuteSteamCmdCommandAsync($"{loginCommand} +quit");
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully logged into Steam as {Username}", username);
                return true;
            }
            else
            {
                _logger.LogError("Failed to log into Steam: {Error}", result.ErrorOutput);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging into Steam");
            return false;
        }
    }

    public async Task<bool> LogoutAsync(string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD path is not configured");
                return false;
            }

            if (!File.Exists(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD executable not found at configured path: {Path}", _settingsProvider.CurrentValue.Server.SteamCMD.Path);
                return false;
            }

            var logoutCommand = $"+logout {username}";

            var result = await ExecuteSteamCmdCommandAsync($"{logoutCommand} +quit");
            
            if (result.Success)
            {
                _logger.LogInformation("Successfully logged out of the Steam account {Username}", username);
                return true;
            }
            else
            {
                _logger.LogError("Failed to log out of Steam: {Error}", result.ErrorOutput);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging out of Steam");
            return false;
        }
    }
    
    public async Task<bool> InstallContentAsync(uint appId, string installDirectory, string username)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD path is not configured");
                return false;
            }

            if (!File.Exists(_settingsProvider.CurrentValue.Server.SteamCMD.Path))
            {
                _logger.LogError("SteamCMD executable not found at configured path: {Path}", _settingsProvider.CurrentValue.Server.SteamCMD.Path);
                return false;
            }

            // Ensure install directory exists
            Directory.CreateDirectory(installDirectory);

            var commands = new List<string>();

            // Add login command if credentials provided
            if (!string.IsNullOrWhiteSpace(username))
            {
                var loginCommand = $"+login {username}";
                
                commands.Add(loginCommand);
            }
            else
            {
                commands.Add("+login anonymous");
            }

            // Add install commands
            commands.Add($"+force_install_dir \"{installDirectory}\"");
            commands.Add($"+app_update {appId} validate");
            commands.Add("+quit");

            var commandString = string.Join(" ", commands);
            var result = await ExecuteSteamCmdCommandAsync(commandString);

            if (result.Success)
            {
                _logger.LogInformation("Successfully installed Steam app {AppId} to {InstallDirectory}", appId, installDirectory);
                return true;
            }
            else
            {
                _logger.LogError("Failed to install Steam app {AppId}: {Error}", appId, result.ErrorOutput);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing Steam content for app {AppId}", appId);
            return false;
        }
    }

    public async Task<bool> RemoveContentAsync(string installDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
            {
                _logger.LogError("Install directory is not specified");
                return false;
            }

            if (!Directory.Exists(installDirectory))
            {
                _logger.LogWarning("Install directory does not exist: {InstallDirectory}", installDirectory);
                return true; // Consider it already removed
            }

            // Remove the directory and all its contents
            Directory.Delete(installDirectory, true);
            
            _logger.LogInformation("Successfully removed content from {InstallDirectory}", installDirectory);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing content from {InstallDirectory}", installDirectory);
            return false;
        }
    }

    private async Task<SteamCmdResult> ExecuteSteamCmdCommandAsync(string arguments, string steamCmdPath = "")
    {
        try
        {
            var executablePath = String.IsNullOrWhiteSpace(steamCmdPath) ? _settingsProvider.CurrentValue.Server.SteamCMD.Path : steamCmdPath;
            
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
                    _logger.LogDebug("SteamCMD Output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                    _logger.LogDebug("SteamCMD Error: {Error}", e.Data);
                }
            };

            process.Start();
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            await process.WaitForExitAsync();

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
            _logger.LogError(ex, "Error executing SteamCMD command: {Arguments}", arguments);
            
            return new SteamCmdResult
            {
                Success = false,
                ExitCode = -1,
                ErrorOutput = ex.Message
            };
        }
    }

    private class SteamCmdResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string ErrorOutput { get; set; } = string.Empty;
    }
}