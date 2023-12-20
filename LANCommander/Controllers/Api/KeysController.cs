using AutoMapper;
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
        private readonly IMapper Mapper;
        private readonly KeyService KeyService;

        public KeysController(IMapper mapper, KeyService keyService)
        {
            Mapper = mapper;
            KeyService = keyService;
        }

        [HttpPost]
        public SDK.Models.Key Get(KeyRequest keyRequest)
        {
            return Mapper.Map<SDK.Models.Key>(KeyService.Get(k => k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress).First());
        }

        [HttpPost("GetAllocated/{id}")]
        public async Task<SDK.Models.Key> GetAllocated(Guid id, KeyRequest keyRequest)
        {
            var existing = KeyService.Get(k => k.Game.Id == id && k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress).FirstOrDefault();

            if (existing != null)
                return Mapper.Map<SDK.Models.Key>(existing);
            else
                return Mapper.Map<SDK.Models.Key>(await AllocateNewKey(id, keyRequest));
        }

        [HttpPost("Allocate/{id}")]
        public async Task<SDK.Models.Key> Allocate(Guid id, KeyRequest keyRequest)
        {
            var existing = KeyService.Get(k => k.Game.Id == id && k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && keyRequest.MacAddress == keyRequest.MacAddress).FirstOrDefault();                

            var availableKey = KeyService.Get(k => k.Game.Id == id)
                .Where(k =>
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null && existing != null)
                return Mapper.Map<SDK.Models.Key>(existing);
            else if (availableKey == null)
                return null;
            else
            {
                await KeyService.Release(existing.Id);

                return Mapper.Map<SDK.Models.Key>(await KeyService.Allocate(availableKey, keyRequest.MacAddress));
            }
        }

        private async Task<SDK.Models.Key> AllocateNewKey(Guid id, KeyRequest keyRequest)
        {
            var availableKey = KeyService.Get(k => k.Game.Id == id)
                .Where(k =>
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == Data.Models.KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null)
                return null;

            return Mapper.Map<SDK.Models.Key>(await KeyService.Allocate(availableKey, keyRequest.MacAddress));
        }
    }
}
