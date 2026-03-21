using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Models;

namespace LANCommander.SDK.Providers;

public class SteamCmdProfileStore(ISettingsProvider settingsProvider) : ISteamCmdProfileStore
{
    public async Task<IEnumerable<SteamCmdProfile>> GetAllAsync()
        => settingsProvider.CurrentValue.Steam.Profiles;

    public async Task<SteamCmdProfile?> GetByUsernameAsync(string username)
        => settingsProvider.CurrentValue.Steam.Profiles.FirstOrDefault(p => p.Username == username);

    public async Task SaveAsync(SteamCmdProfile profile)
    {
        settingsProvider.Update(s =>
        {
            var existing = s.Steam.Profiles.FirstOrDefault(p => p.Username == profile.Username);
            
            if (existing != null)
            {
                // Update existing profile
                existing.InstallDirectory = profile.InstallDirectory;
            }
            else
            {
                // Add new profile
                s.Steam.Profiles.Add(profile);
            }
        });
    }

    public async Task DeleteAsync(string username)
    {
        settingsProvider.Update(s =>
        {
            var existing = s.Steam.Profiles.FirstOrDefault(p => p.Username == username);
            
            if (existing != null)
            {
                s.Steam.Profiles.Remove(existing);
            }
        });
    }
}