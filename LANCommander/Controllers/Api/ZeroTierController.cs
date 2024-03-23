using LANCommander.Data.Models;
using LANCommander.Models;
using LANCommander.SDK.VPN.Models.ZeroTier;
using LANCommander.Services;
using LANCommander.Services.VPNServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Controllers.Api
{
    [Authorize(AuthenticationSchemes = "Bearer")]
    [Route("api/[controller]")]
    [ApiController]
    public class ZeroTierController : ControllerBase
    {
        private readonly ZeroTierService ZeroTierService;
        private readonly UserManager<User> UserManager;
        private readonly RoleManager<Role> RoleManager;

        public ZeroTierController(ZeroTierService zeroTierService, UserManager<User> userManager, RoleManager<Role> roleManager)
        { 
            ZeroTierService = zeroTierService;
            UserManager = userManager;
            RoleManager = roleManager;
        }

        [HttpPost]
        public async Task<IActionResult> ApproveNode(ApproveNodeRequest request)
        {
            var settings = SettingService.GetSettings();

            var user = await UserManager.FindByNameAsync(User.Identity.Name);
            var roleNames = await UserManager.GetRolesAsync(user);

            bool allowed = false;

            foreach (var roleName in roleNames)
            {
                var role = await RoleManager.FindByNameAsync(roleName);

                if (role != null && settings.VPN.AllowedRoles.Contains(role.Id))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
                return Forbid();

            var config = settings.VPN.Configuration as LANCommanderZeroTierSettings;

            await ZeroTierService.ApproveNode(config.NetworkId, user.UserName);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> RemoveNode(RemoveNodeRequest request)
        {
            var settings = SettingService.GetSettings();

            var user = await UserManager.FindByNameAsync(User.Identity.Name);

            // Do we add any check here that the user is authorized to do VPN stuff?
            // We should at least do a check that they own that 

            await ZeroTierService.RemoveNode(request.NodeId);

            return Ok();
        }
    }
}
