using AutoMapper;
using LANCommander.SDK.Enums;
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
        IFusionCache cache,
        IMapper mapper,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<Key>(logger, cache, mapper, httpContextAccessor, contextFactory)
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
            key.ClaimedByUser = user;
            key.ClaimedOn = DateTime.UtcNow;
            key.AllocationMethod = KeyAllocationMethod.UserAccount;

            key = await UpdateAsync(key);

            return key;
        }

        public async Task<Key> AllocateAsync(Key key, string macAddress)
        {
            key.ClaimedByMacAddress = macAddress;
            key.ClaimedOn = DateTime.UtcNow;
            key.AllocationMethod = KeyAllocationMethod.MacAddress;

            key = await UpdateAsync(key);

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
            switch (key.AllocationMethod)
            {
                case KeyAllocationMethod.UserAccount:
                    key.ClaimedByUser = null;
                    key.ClaimedOn = null;
                    break;

                case KeyAllocationMethod.MacAddress:
                    key.ClaimedByMacAddress = "";
                    key.ClaimedOn = null;
                    break;
            }

            return await UpdateAsync(key);
        }
    }
}
