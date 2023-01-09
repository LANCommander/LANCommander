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
    }
}
