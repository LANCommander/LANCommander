using Semver;
using System.Threading.Tasks;

namespace LANCommander.SDK.Migrations;

public interface IMigration
{
    /// <summary>
    /// The version of the Migration.
    /// </summary>
    public SemVersion Version { get; }

    /// <summary>
    /// Asynchronously determines whether the associated operation should be executed.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the operation
    /// should be executed; otherwise, <see langword="false"/>.</returns>
    public Task<bool> ShouldExecuteAsync();

    /// <summary>
    /// Performs any necessary pre-checks before executing the migration.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result is <see lang="true"/> if pre-checks pass; otherwise, <see langword="false"/>.</returns>
    public Task<bool> PerformPreChecksAsync();

    /// <summary>
    /// Executes the migration asynchronously.
    /// </summary>
    /// <remarks>
    /// This will only be called if <see cref="ShouldExecuteAsync"/> returns <see langword="true"/> and <see cref="PerformPreChecksAsync"/> returns <see langword="true"/>.
    /// </remarks>
    public Task ExecuteAsync();

}
