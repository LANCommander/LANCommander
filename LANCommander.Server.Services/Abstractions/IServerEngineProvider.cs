namespace LANCommander.Server.Services.Abstractions;

/// <summary>
/// Supplies server engines from outside this assembly, e.g. those plugins register, to
/// <see cref="ServerManager"/> alongside the built-in <see cref="IServerEngine"/>s.
/// </summary>
public interface IServerEngineProvider
{
    IEnumerable<IServerEngine> GetEngines();
}
