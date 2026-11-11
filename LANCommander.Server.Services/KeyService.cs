using LANCommander.SDK.Enums;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class KeyService(
        ILogger<KeyService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory,
        IPluginEventBus pluginEventBus) : BaseDatabaseService<Key>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        public override async Task<Key> AddAsync(Key entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(k => k.ClaimedByUser);
                await context.UpdateRelationshipAsync(k => k.Game);
            });
        }

        public override async Task<Key> UpdateAsync(Key entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(k => k.ClaimedByUser);
                await context.UpdateRelationshipAsync(k => k.Game);
            });
        }
        
        public async Task<Key> AllocateAsync(Key key, User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            key.ClaimedByUser = user;
            key.ClaimedByUserId = user.Id;
            key.ClaimedOn = DateTime.UtcNow;
            key.AllocationMethod = KeyAllocationMethod.UserAccount;

            key = await UpdateAsync(key);

            await pluginEventBus.PublishAsync(new KeyAllocatedEvent(
                key.Id, key.GameId, KeyAllocationMethod.UserAccount, user.Id, null, await CountAvailableAsync(key.GameId)));

            return key;
        }

        public async Task<Key> AllocateAsync(Key key, string macAddress)
        {
            if (String.IsNullOrWhiteSpace(macAddress))
                throw new ArgumentException("A MAC address is required to allocate a key by MAC address.", nameof(macAddress));

            key.ClaimedByMacAddress = macAddress;
            key.ClaimedOn = DateTime.UtcNow;
            key.AllocationMethod = KeyAllocationMethod.MacAddress;

            key = await UpdateAsync(key);

            await pluginEventBus.PublishAsync(new KeyAllocatedEvent(
                key.Id, key.GameId, KeyAllocationMethod.MacAddress, null, macAddress, await CountAvailableAsync(key.GameId)));

            return key;
        }

        public async Task<Key> ReleaseAsync(Guid id)
        {
            var key = await GetAsync(id);

            if (key == null)
                return null;

            return await ReleaseAsync(key);
        }

        public async Task<Key> ReleaseAsync(Key key)
        {
            var wasAllocated = key.IsAllocated();
            var userId = key.AllocationMethod == KeyAllocationMethod.UserAccount ? key.ClaimedByUserId : null;
            var macAddress = key.AllocationMethod == KeyAllocationMethod.MacAddress ? key.ClaimedByMacAddress : null;

            key.ClaimedByUser = null;
            key.ClaimedByUserId = null;
            key.ClaimedByMacAddress = null;
            key.ClaimedOn = null;
            key.AllocationMethod = null;

            key = await UpdateAsync(key);

            if (wasAllocated)
                await pluginEventBus.PublishAsync(new KeyReleasedEvent(
                    key.Id, key.GameId, userId, macAddress, await CountAvailableAsync(key.GameId)));

            return key;
        }

        // Mirrors Key.IsAvailable, which EF can't translate.
        private async Task<int> CountAvailableAsync(Guid gameId)
        {
            await using var context = await contextFactory.CreateDbContextAsync();

            return await context.Set<Key>().CountAsync(k => k.GameId == gameId
                && !(k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress != null && k.ClaimedByMacAddress.Trim() != ""
                    || k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUserId != null));
        }
    }
}
