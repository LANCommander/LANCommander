using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services;

/// <summary>
/// Ensures only one launcher instance runs at a time and lets a second instance
/// (started to open a <c>lancommander://</c> link, or relaunched by a user who forgot the
/// launcher was in the tray) hand its request to the already-running process via a named pipe.
///
/// Messages:  "restore", "open-url:{url}"
/// </summary>
public class SingleInstanceService : IDisposable
{
    private const string PipeName  = "lancommander-launcher";
    private const string MutexName = "lancommander-launcher-singleton";
    private const string OpenUrlPrefix = "open-url:";

    // Held for the lifetime of the process to mark this as the owning instance.
    private static Mutex? _instanceMutex;

    private readonly ILogger<SingleInstanceService> _logger;
    private CancellationTokenSource? _cts;

    /// <summary>Raised with the link a secondary instance was started to open.</summary>
    public event EventHandler<string>? OpenUrlRequested;

    /// <summary>Raised when a secondary instance asks the running one to surface.</summary>
    public event EventHandler? RestoreRequested;

    public SingleInstanceService(ILogger<SingleInstanceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Claims the single-instance lock for this process. Returns true when this is the first
    /// instance, false when another instance already holds the lock. The mutex is intentionally
    /// kept alive for the lifetime of the process and released by the OS on exit.
    /// </summary>
    public static bool TryAcquireInstanceLock()
    {
        _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        return createdNew;
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
        if (message.Equals("restore", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Restore request received from a secondary instance");
            RestoreRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (message.StartsWith(OpenUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var url = message[OpenUrlPrefix.Length..].Trim();

            if (url.Length > 0)
                OpenUrlRequested?.Invoke(this, url);
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

    /// <summary>The message that asks the running instance to open <paramref name="url"/>.</summary>
    public static string OpenUrlMessage(string url) => OpenUrlPrefix + url;

    public void Dispose() => _cts?.Cancel();
}
