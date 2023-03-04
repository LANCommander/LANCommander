using LANCommander.Data;
using LANCommander.Data.Models;

namespace LANCommander.Services
{
    public class KeyService : BaseDatabaseService<Key>
    {
        public KeyService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }

        public async Task<Key> Allocate(Key key, User user)
        {
            key.ClaimedByUser = user;
            key.AllocationMethod = KeyAllocationMethod.UserAccount;

            key = await Update(key);

            return key;
        }

        public async Task<Key> Allocate(Key key, string macAddress)
        {
            key.ClaimedByMacAddress = macAddress;
            key.AllocationMethod = KeyAllocationMethod.MacAddress;

            key = await Update(key);

            return key;
        }

        public async Task<Key> Release(Guid id)
        {
            var key = await Get(id);

            if (key == null)
                return null;

            return await Release(key);
        }

        public async Task<Key> Release(Key key)
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

            return await Update(key);
        }
    }
}
