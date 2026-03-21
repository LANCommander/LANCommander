using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.Steam.Implementations;

/// <summary>
/// In-memory implementation of ISteamCmdProfileStore
/// Useful for testing or simple scenarios
/// </summary>
public class InMemorySteamCmdProfileStore : ISteamCmdProfileStore
{
    private readonly Dictionary<string, SteamCmdProfile> _profiles = new();

    public Task<IEnumerable<SteamCmdProfile>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<SteamCmdProfile>>(_profiles.Values.ToList());
    }

    public Task<SteamCmdProfile?> GetByUsernameAsync(string username)
    {
        _profiles.TryGetValue(username, out var profile);
        return Task.FromResult(profile);
    }

    public Task SaveAsync(SteamCmdProfile profile)
    {
        _profiles[profile.Username] = profile;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string username)
    {
        _profiles.Remove(username);
        return Task.CompletedTask;
    }
}
