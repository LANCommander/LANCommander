using LANCommander.Data.Models;
using LANCommander.Models;
using LANCommander.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NLog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LANCommander.Controllers.Api
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly UserManager<User> UserManager;

        public ProfileController(UserManager<User> userManager)
        {
            UserManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                return Ok(user);
            }
            else
                return Unauthorized();
        }

        [HttpPost("ChangeAlias")]
        public async Task<IActionResult> ChangeAlias(ChangeAliasRequest request)
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                user.Alias = request.Alias;

                await UserManager.UpdateAsync(user);

                return Ok(request.Alias);
            }
            else
                return Unauthorized();
        }
    }
}
