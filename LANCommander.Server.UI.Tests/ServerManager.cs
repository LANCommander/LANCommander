using System.Diagnostics;
using System.Net;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Manages the LANCommander server process lifecycle for UI tests.
/// Can start a fresh server (no existing data) or one with pre-existing data.
/// </summary>
public class ServerManager : IAsyncDisposable
{
    private Process? _serverProcess;
    private readonly string _serverProjectPath;
    private readonly string _dataDirectory;
    private bool _cleanedUp;

    public ServerManager(string? dataDirectory = null)
    {
        _serverProjectPath = FindServerProjectPath();
        _dataDirectory = dataDirectory ?? Path.Combine(
            Path.GetDirectoryName(_serverProjectPath)!,
            "Data");
    }

    public string DataDirectory => _dataDirectory;

    /// <summary>
    /// Ensures the server has a clean data directory (no existing config/database).
    /// </summary>
    public void EnsureCleanState()
    {
        var serverDir = Path.GetDirectoryName(_serverProjectPath)!;
        var dataDir = Path.Combine(serverDir, "Data");
        var configDir = Path.Combine(serverDir, "config");

        if (Directory.Exists(dataDir))
        {
            var backupDir = dataDir + ".uitest-bak";
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
            Directory.Move(dataDir, backupDir);
        }

        if (Directory.Exists(configDir))
        {
            var backupDir = configDir + ".uitest-bak";
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);
            Directory.Move(configDir, backupDir);
        }
    }

    /// <summary>
    /// Restores the original data/config directories that were backed up.
    /// </summary>
    public void RestoreOriginalState()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;

        var serverDir = Path.GetDirectoryName(_serverProjectPath)!;

        RestoreDirectory(Path.Combine(serverDir, "Data"));
        RestoreDirectory(Path.Combine(serverDir, "config"));
    }

    private static void RestoreDirectory(string dir)
    {
        var backupDir = dir + ".uitest-bak";
        if (Directory.Exists(backupDir))
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.Move(backupDir, dir);
        }
    }

    /// <summary>
    /// Starts the LANCommander server and waits for it to be ready.
    /// </summary>
    public async Task StartAsync(TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(120);

        _serverProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{_serverProjectPath}\" --no-build",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        _serverProcess.Start();

        // Wait for the server to be ready by polling the URL
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var response = await httpClient.GetAsync(TestConstants.BaseUrl);
                if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
                    return; // Server is ready
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException(
            $"Server did not start within {timeout.Value.TotalSeconds} seconds");
    }

    public async Task StopAsync()
    {
        if (_serverProcess is { HasExited: false })
        {
            _serverProcess.Kill(entireProcessTree: true);
            await _serverProcess.WaitForExitAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        RestoreOriginalState();
        _serverProcess?.Dispose();
    }

    private static string FindServerProjectPath()
    {
        // Walk up from the test assembly location to find the solution root
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "LANCommander.Server", "LANCommander.Server.csproj");
            if (File.Exists(candidate))
                return candidate;

            // Also check if we're in the solution root
            var slnx = Path.Combine(dir, "LANCommander.slnx");
            if (File.Exists(slnx))
            {
                candidate = Path.Combine(dir, "LANCommander.Server", "LANCommander.Server.csproj");
                if (File.Exists(candidate))
                    return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("Could not find LANCommander.Server.csproj");
    }
}
