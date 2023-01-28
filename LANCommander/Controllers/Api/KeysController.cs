using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Extensions;
using LANCommander.SDK.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class KeysController : ControllerBase
    {
        private KeyService KeyService;

        public KeysController(KeyService keyService)
        {
            KeyService = keyService;
        }

        [HttpPost]
        public Data.Models.Key Get(KeyRequest keyRequest)
        {
            return KeyService.Get(k => k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress).First();
        }

        public async Task<Data.Models.Key> GetAllocated(Guid id, KeyRequest keyRequest)
        {
            var existing = KeyService.Get(k => k.Game.Id == id && k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress).FirstOrDefault();

            if (existing != null)
                return existing;
            else
                return await AllocateNewKey(id, keyRequest);
        }

        [HttpPost("Allocate/{id}")]
        public async Task<Data.Models.Key> Allocate(Guid id, KeyRequest keyRequest)
        {
            var existing = KeyService.Get(k => k.Game.Id == id && k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && keyRequest.MacAddress == keyRequest.MacAddress).FirstOrDefault();                

            var availableKey = KeyService.Get(k => k.Game.Id == id)
                .Where(k =>
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null && existing != null)
                return existing;
            else if (availableKey == null)
                return null;
            else
            {
                await KeyService.Release(existing.Id);

                return await KeyService.Allocate(availableKey, keyRequest.MacAddress);
            }
        }

        private async Task<Data.Models.Key> AllocateNewKey(Guid id, KeyRequest keyRequest)
        {
            var availableKey = KeyService.Get(k => k.Game.Id == id)
                .Where(k =>
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null)
                return null;

            return await KeyService.Allocate(availableKey, keyRequest.MacAddress);
        }
    }
}
