using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.Steam.Models;

namespace LANCommander.Steam.Abstractions;

/// <summary>
/// Interface for storing and retrieving SteamCMD profiles
/// Allows consumers to implement their own storage mechanism
/// </summary>
public interface ISteamCmdProfileStore
{
    /// <summary>
    /// Get all profiles
    /// </summary>
    Task<IEnumerable<SteamCmdProfile>> GetAllAsync();

    /// <summary>
    /// Get a profile by username
    /// </summary>
    Task<SteamCmdProfile?> GetByUsernameAsync(string username);

    /// <summary>
    /// Save or update a profile
    /// </summary>
    Task SaveAsync(SteamCmdProfile profile);

    /// <summary>
    /// Delete a profile by username
    /// </summary>
    Task DeleteAsync(string username);
}
