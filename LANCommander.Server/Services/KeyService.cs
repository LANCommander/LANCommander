﻿using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services
{
    public class KeyService : BaseDatabaseService<Key>
    {
        public KeyService(
            ILogger<CollectionService> logger,
            DatabaseContext dbContext) : base(logger, dbContext) { }

        public async Task<Key> Allocate(Key key, User user)
        {
            key.ClaimedByUser = user;
            key.ClaimedOn = DateTime.Now;
            key.AllocationMethod = KeyAllocationMethod.UserAccount;

            key = await Update(key);

            return key;
        }

        public async Task<Key> Allocate(Key key, string macAddress)
        {
            key.ClaimedByMacAddress = macAddress;
            key.ClaimedOn = DateTime.Now;
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
