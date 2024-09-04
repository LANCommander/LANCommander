using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using LANCommander.Server.Models;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class KeysController : BaseApiController
    {
        private readonly IMapper Mapper;
        private readonly KeyService KeyService;
        private readonly GameService GameService;
        private readonly UserManager<Data.Models.User> UserManager;

        public KeysController(
            ILogger<KeysController> logger,
            IMapper mapper,
            KeyService keyService,
            GameService gameService,
            UserManager<Data.Models.User> userManager) : base(logger)
        {
            Mapper = mapper;
            KeyService = keyService;
            GameService = gameService;
            UserManager = userManager;
        }

        [HttpPost]
        public SDK.Models.Key Get(KeyRequest keyRequest)
        {
            return Mapper.Map<SDK.Models.Key>(KeyService.Get(k => k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress).First());
        }

        /// <summary>
        /// Get allocated key (or allocate new key) based on game's key allocation method
        /// </summary>
        /// <param name="id">ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <returns>Allocated key</returns>
        [HttpPost("GetAllocated/{id}")]
        public async Task<SDK.Models.Key> GetAllocated(Guid id, KeyRequest keyRequest)
        {
            Data.Models.Key key = null;

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var game = await GameService.Get(id);

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser?.Id == user.Id);
                    break;
            }

            if (key != null)
                return Mapper.Map<SDK.Models.Key>(key);
            else
                return Mapper.Map<SDK.Models.Key>(await AllocateNewKey(id, keyRequest, game.KeyAllocationMethod));
        }

        /// <summary>
        /// Allocate a new key based on game's key allocation method
        /// </summary>
        /// <param name="id">ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <returns>Newly allocated key</returns>
        [HttpPost("Allocate/{id}")]
        public async Task<SDK.Models.Key> Allocate(Guid id, KeyRequest keyRequest)
        {
            Data.Models.Key key = null;

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var game = await GameService.Get(id);

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser?.Id == user.Id);
                    break;
            }

            var availableKey = game.Keys.FirstOrDefault(k =>
                (k.AllocationMethod == KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null));

            if (availableKey == null && key != null)
                return Mapper.Map<SDK.Models.Key>(key);
            else if (availableKey == null)
                return null;
            else
            {
                if (key != null)
                    await KeyService.Release(key.Id);

                switch (game.KeyAllocationMethod)
                {
                    case KeyAllocationMethod.MacAddress:
                        key = await KeyService.Allocate(availableKey, keyRequest.MacAddress);
                        break;

                    case KeyAllocationMethod.UserAccount:
                        key = await KeyService.Allocate(availableKey, user);
                        break;
                }

                return Mapper.Map<SDK.Models.Key>(key);
            }
        }

        /// <summary>
        /// Allocate a new key using specified allocation method
        /// </summary>
        /// <param name="id">The ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <param name="keyAllocationMethod"></param>
        /// <returns>Allocated key</returns>
        private async Task<SDK.Models.Key> AllocateNewKey(Guid id, KeyRequest keyRequest, KeyAllocationMethod keyAllocationMethod)
        {
            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            var availableKey = KeyService.Get(k => k.Game.Id == id)
                .Where(k =>
                (k.AllocationMethod == KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null)
                return null;

            if (keyAllocationMethod == KeyAllocationMethod.MacAddress)
                return Mapper.Map<SDK.Models.Key>(await KeyService.Allocate(availableKey, keyRequest.MacAddress));
            else if (keyAllocationMethod == KeyAllocationMethod.UserAccount)
                return Mapper.Map<SDK.Models.Key>(await KeyService.Allocate(availableKey, user));
            else
                return null;
        }
    }
}
