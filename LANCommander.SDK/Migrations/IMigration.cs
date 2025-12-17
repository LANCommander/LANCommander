using Semver;
using System.Threading.Tasks;

namespace LANCommander.SDK.Migrations;

public interface IMigration
{
    public SemVersion Version { get; }
    public Task ExecuteAsync();
}
