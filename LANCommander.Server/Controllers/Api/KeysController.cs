using AutoMapper;
using LANCommander.Server.Services;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        private readonly UserService UserService;

        public KeysController(
            ILogger<KeysController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            IMapper mapper,
            KeyService keyService,
            GameService gameService,
            UserService userService) : base(logger, settingsProvider)
        {
            Mapper = mapper;
            KeyService = keyService;
            GameService = gameService;
            UserService = userService;
        }

        [HttpPost]
        public async Task<ActionResult<SDK.Models.Key>> GetAsync(KeyRequest keyRequest)
        {
            return await GetAllocatedAsync(keyRequest.GameId, keyRequest);
        }

        /// <summary>
        /// Get allocated key (or allocate new key) based on game's key allocation method
        /// </summary>
        /// <param name="id">ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <returns>Allocated key</returns>
        [HttpPost("GetAllocated/{id}")]
        public async Task<ActionResult<SDK.Models.Key>> GetAllocatedAsync(Guid id, KeyRequest keyRequest)
        {
            try
            {
                Data.Models.Key key = null;

                var user = await UserService.GetAsync(User?.Identity?.Name);
                var game = await GameService
                    .Include(g => g.Keys)
                    .GetAsync(id);

                if (game == null)
                {
                    Logger.LogError("Requested game with ID {GameId} does not exist", keyRequest.GameId);
                    return NotFound();
                }

                switch (game.KeyAllocationMethod)
                {
                    case KeyAllocationMethod.MacAddress:
                        key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress);
                        break;

                    case KeyAllocationMethod.UserAccount:
                        key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser?.Id == user.Id);
                        break;

                    default:
                        Logger?.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                        return NotFound();
                        break;
                }

                if (key != null)
                    return Ok(Mapper.Map<SDK.Models.Key>(key));
                else
                    return Ok(Mapper.Map<SDK.Models.Key>(await AllocateNewKeyAsync(id, keyRequest, game.KeyAllocationMethod)));
            }
            catch (Exception ex) {
                Logger?.LogError(ex, "An unknown error occurred while trying to get an allocated key for game with ID {GameId}", id);

                return NotFound();
            }
        }

        /// <summary>
        /// Allocate a new key based on game's key allocation method
        /// </summary>
        /// <param name="id">ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <returns>Newly allocated key</returns>
        [HttpPost("Allocate/{id}")]
        public async Task<ActionResult<SDK.Models.Key>> AllocateAsync(Guid id, KeyRequest keyRequest)
        {
            try
            {
                Data.Models.Key key = null;

                var user = await UserService.GetAsync(User?.Identity?.Name);
                var game = await GameService
                    .Include(g => g.Keys)
                    .GetAsync(id);

                if (game == null)
                {
                    Logger.LogError("Requested game with ID {GameId} does not exist", keyRequest.GameId);
                    return NotFound();
                }

                switch (game.KeyAllocationMethod)
                {
                    case KeyAllocationMethod.MacAddress:
                        key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.MacAddress && k.ClaimedByMacAddress == keyRequest.MacAddress);
                        break;

                    case KeyAllocationMethod.UserAccount:
                        key = game.Keys.FirstOrDefault(k => k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser?.Id == user.Id);
                        break;

                    default:
                        Logger?.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                        return NotFound();
                        break;
                }

                var availableKey = game.Keys.FirstOrDefault(k =>
                    (k.AllocationMethod == KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                    ||
                    (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null));

                if (availableKey == null && key != null)
                    return Ok(Mapper.Map<SDK.Models.Key>(key));
                else if (availableKey == null)
                    return NotFound();
                else
                {
                    if (key != null)
                        await KeyService.ReleaseAsync(key.Id);

                    switch (game.KeyAllocationMethod)
                    {
                        case KeyAllocationMethod.MacAddress:
                            key = await KeyService.AllocateAsync(availableKey, keyRequest.MacAddress);
                            break;

                        case KeyAllocationMethod.UserAccount:
                            key = await KeyService.AllocateAsync(availableKey, user);
                            break;
                    }

                    return Ok(Mapper.Map<SDK.Models.Key>(key));
                }
            }
            catch (Exception ex) {
                Logger.LogError(ex, "An unknown error occurred while trying to allocate a new key for game with ID {GameId}", id);

                return NotFound();
            }
        }

        /// <summary>
        /// Allocate a new key using specified allocation method
        /// </summary>
        /// <param name="id">The ID of the game</param>
        /// <param name="keyRequest"></param>
        /// <param name="keyAllocationMethod"></param>
        /// <returns>Allocated key</returns>
        private async Task<SDK.Models.Key> AllocateNewKeyAsync(Guid id, KeyRequest keyRequest, KeyAllocationMethod keyAllocationMethod)
        {
            var user = await UserService.GetAsync(User?.Identity?.Name);

            var keys = await KeyService.GetAsync(k => k.GameId == id);
            var availableKey = keys.Where(k =>
                (k.AllocationMethod == KeyAllocationMethod.MacAddress && String.IsNullOrWhiteSpace(k.ClaimedByMacAddress))
                ||
                (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null))
                .FirstOrDefault();

            if (availableKey == null)
                return null;

            if (keyAllocationMethod == KeyAllocationMethod.MacAddress)
                return Mapper.Map<SDK.Models.Key>(await KeyService.AllocateAsync(availableKey, keyRequest.MacAddress));
            else if (keyAllocationMethod == KeyAllocationMethod.UserAccount)
                return Mapper.Map<SDK.Models.Key>(await KeyService.AllocateAsync(availableKey, user));
            else
                return null;
        }
    }
}
