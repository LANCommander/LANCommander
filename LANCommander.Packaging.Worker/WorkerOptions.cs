namespace LANCommander.Packaging.Worker;

/// <summary>
/// Command line for a worker process.
/// </summary>
internal sealed class WorkerOptions
{
    public string PipeName { get; init; } = string.Empty;

    /// <summary>One-time secret echoed back during the handshake.</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// The launcher's process id. The worker exits when it goes away, so a launcher crash can
    /// never leave an injector running against the user's installer.
    /// </summary>
    public int HostProcessId { get; init; }

    /// <summary>Explicit path to the native Interposer DLL. Defaults to next to the worker.</summary>
    public string? InterposerDllPath { get; init; }

    /// <summary>
    /// Path of an x86 worker to start as a child.
    /// <para>
    /// Set when the launcher escalated this worker through UAC. Starting the other
    /// architecture's worker from here means it inherits elevation without a second consent
    /// prompt, so a session costs at most one.
    /// </para>
    /// </summary>
    public string? SpawnWorkerPath { get; init; }

    public string? SpawnWorkerPipeName { get; init; }

    public string? SpawnWorkerToken { get; init; }

    public bool HasSpawnRequest =>
        !string.IsNullOrEmpty(SpawnWorkerPath) &&
        !string.IsNullOrEmpty(SpawnWorkerPipeName) &&
        !string.IsNullOrEmpty(SpawnWorkerToken);

    public static WorkerOptions? Parse(string[] args)
    {
        string? pipeName = null;
        string? token = null;
        var hostProcessId = 0;
        string? interposerDllPath = null;
        string? spawnPath = null;
        string? spawnPipe = null;
        string? spawnToken = null;

        for (var i = 0; i < args.Length; i++)
        {
            var hasValue = i + 1 < args.Length;

            switch (args[i])
            {
                case "--pipe" when hasValue:
                    pipeName = args[++i];
                    break;

                case "--token" when hasValue:
                    token = args[++i];
                    break;

                case "--host-pid" when hasValue:
                    _ = int.TryParse(args[++i], out hostProcessId);
                    break;

                case "--interposer-dll" when hasValue:
                    interposerDllPath = args[++i];
                    break;

                case "--spawn-worker" when hasValue:
                    spawnPath = args[++i];
                    break;

                case "--spawn-worker-pipe" when hasValue:
                    spawnPipe = args[++i];
                    break;

                case "--spawn-worker-token" when hasValue:
                    spawnToken = args[++i];
                    break;
            }
        }

        if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(token) || hostProcessId <= 0)
            return null;

        return new WorkerOptions
        {
            PipeName = pipeName,
            Token = token,
            HostProcessId = hostProcessId,
            InterposerDllPath = interposerDllPath,
            SpawnWorkerPath = spawnPath,
            SpawnWorkerPipeName = spawnPipe,
            SpawnWorkerToken = spawnToken,
        };
    }
}
