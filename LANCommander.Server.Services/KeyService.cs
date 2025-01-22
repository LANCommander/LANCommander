using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class KeyService : BaseDatabaseService<Key>
    {
        public KeyService(
            ILogger<KeyService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory) : base(logger, cache, repositoryFactory) { }

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
