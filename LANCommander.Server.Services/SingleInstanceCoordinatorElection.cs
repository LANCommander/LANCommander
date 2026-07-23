using LANCommander.Server.Services.Abstractions;

namespace LANCommander.Server.Services;

/// <summary>
/// Default <see cref="ICoordinatorElection"/> used when horizontal scaling is disabled. There is
/// only one instance, so it is always the coordinator.
/// </summary>
public sealed class SingleInstanceCoordinatorElection : ICoordinatorElection
{
    public bool IsLeader => true;

    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
