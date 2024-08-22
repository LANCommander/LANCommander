using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NLog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ApiController]
    public class ProfileController : ControllerBase
    {
        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly UserManager<User> UserManager;
        private readonly UserService UserService;

        public ProfileController(UserManager<User> userManager, UserService userService)
        {
            UserManager = userManager;
            UserService = userService;
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

        [HttpGet("Avatar")]
        public async Task<IActionResult> Avatar()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                var media = user.Media?.FirstOrDefault(u => u.Type == SDK.Enums.MediaType.Avatar);

                if (media == null)
                    return NotFound();

                var fs = System.IO.File.OpenRead(MediaService.GetImagePath(media));

                return File(fs, media.MimeType);
            }

            return NotFound();
        }

        [AllowAnonymous]
        [HttpGet("{userName}/Avatar")]
        public async Task<IActionResult> Avatar(string userName)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(userName);

                if (user == null)
                    return NotFound();

                var media = user.Media?.FirstOrDefault(u => u.Type == SDK.Enums.MediaType.Avatar);

                if (media == null)
                    return NotFound();

                var fs = System.IO.File.OpenRead(MediaService.GetImagePath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpGet("CustomField/{name}")]
        public async Task<IActionResult> CustomField(string name)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                var field = await UserService.GetCustomField(user.Id, name);

                return Ok(field.Value);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpPost("CustomField/{name}")]
        public async Task<IActionResult> CustomField(string name, string value)
        {
            try
            {
                var user = await UserManager.FindByNameAsync(User.Identity.Name);

                await UserService.UpdateCustomField(user.Id, name, value);

                return Ok(value);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }
    }
}
