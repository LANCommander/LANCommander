using System.Threading.Tasks;
using Semver;

namespace LANCommander.SDK.Migrations;

public interface IMigration
{
    public SemVersion Version { get; }
    public Task ExecuteAsync();
}