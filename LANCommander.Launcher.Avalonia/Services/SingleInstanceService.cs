using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace LANCommander.Launcher.Avalonia.Services;

/// <summary>
/// Ensures only one launcher instance runs at a time and lets a second instance
/// (spawned when the user clicks a notification) pass a navigation request to
/// the already-running process via a named pipe.
///
/// Protocol:  "navigate-game:{guid}"
/// </summary>
public class SingleInstanceService : IDisposable
{
    private const string PipeName       = "lancommander-launcher";
    private const string ProtocolScheme = "lancommander";

    private readonly ILogger<SingleInstanceService> _logger;
    private CancellationTokenSource? _cts;

    public event EventHandler<Guid>? NavigateToGameRequested;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
    {
        _logger = logger;
    }

    // ── Server (first instance) ──────────────────────────────────────────────

    /// <summary>Start listening for messages from secondary instances.</summary>
    public void StartServer()
    {
        _cts = new CancellationTokenSource();
        _ = ListenLoopAsync(_cts.Token);
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8);
                var message = await reader.ReadToEndAsync(ct).ConfigureAwait(false);

                HandleMessage(message);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Named pipe server error");
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }
        }
    }

    private void HandleMessage(string message)
    {
        if (message.StartsWith("navigate-game:", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = message["navigate-game:".Length..].Trim();
            if (Guid.TryParse(idStr, out var gameId))
            {
                _logger.LogInformation("Navigation request received for game {GameId}", gameId);
                NavigateToGameRequested?.Invoke(this, gameId);
            }
        }
    }

    // ── Client (secondary instance) ──────────────────────────────────────────

    /// <summary>
    /// Try to send a message to the already-running instance.
    /// Returns true if the message was delivered.
    /// </summary>
    public static bool TrySendToServer(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(timeout: 2000);
            using var writer = new StreamWriter(client, Encoding.UTF8, leaveOpen: true);
            writer.Write(message);
            writer.Flush();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parse a protocol URL of the form <c>lancommander://game/{guid}</c>.
    /// Returns the game GUID if parsed successfully.
    /// </summary>
    public static Guid? ParseProtocolArg(string arg)
    {
        var prefix = $"{ProtocolScheme}://game/";
        if (!arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;
        var idStr = arg[prefix.Length..].TrimEnd('/');
        return Guid.TryParse(idStr, out var id) ? id : null;
    }

    // ── Windows protocol registration ────────────────────────────────────────

    /// <summary>
    /// Registers <c>lancommander://</c> in HKCU (no elevation required) so
    /// Windows routes toast-notification click-actions to this executable.
    /// No-op on non-Windows.
    /// </summary>
    public void RegisterProtocolHandler()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            var exePath = Environment.ProcessPath ?? AppContext.BaseDirectory;

            using var key = Registry.CurrentUser.CreateSubKey(
                $@"Software\Classes\{ProtocolScheme}");
            key.SetValue("", "URL:LANCommander Launcher");
            key.SetValue("URL Protocol", "");

            using var cmd = key.CreateSubKey(@"shell\open\command");
            cmd.SetValue("", $"\"{exePath}\" \"%1\"");

            _logger.LogInformation("Registered {Scheme}:// protocol handler", ProtocolScheme);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register protocol handler");
        }
    }

    public void Dispose() => _cts?.Cancel();
}
