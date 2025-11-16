using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Controllers.Api
{
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    [ApiController]
    public class ProfileController : BaseApiController
    {
        private readonly UserService UserService;
        private readonly MediaService MediaService;
        private readonly UserCustomFieldService UserCustomFieldService;

        public ProfileController(
            ILogger<ProfileController> logger,
            SettingsProvider<Settings.Settings> settingsProvider,
            UserService userService,
            MediaService mediaService,
            UserCustomFieldService userCustomFieldService) : base(logger, settingsProvider)
        {
            UserService = userService;
            MediaService = mediaService;
            UserCustomFieldService = userCustomFieldService;
        }

        [HttpGet]
        public async Task<ActionResult<User>> GetAsync()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserService.GetAsync<SDK.Models.User>(User?.Identity?.Name);

                if (user != null)
                    return Ok(user);
                else
                    return NotFound();
            }
            else
                return Unauthorized();
        }

        [HttpPut("ChangeAlias")]
        public async Task<IActionResult> ChangeAliasAsync(ChangeAliasRequest request)
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserService.GetAsync(User?.Identity?.Name);

                user.Alias = request.Alias;

                await UserService.UpdateAsync(user);

                return Ok(request.Alias);
            }
            else
                return Unauthorized();
        }

        [HttpGet("Avatar")]
        public async Task<IActionResult> AvatarAsync()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var user = await UserService.GetAsync(User?.Identity?.Name);

                if (user == null)
                    return NotFound();

                var media = await MediaService.FirstOrDefaultAsync(m => m.Type == SDK.Enums.MediaType.Avatar && m.UserId == user.Id);

                if (media == null)
                    return NotFound();

                var fs = System.IO.File.OpenRead(MediaService.GetMediaPath(media));

                return File(fs, media.MimeType);
            }

            return NotFound();
        }

        [AllowAnonymous]
        [HttpGet("{userName}/Avatar")]
        public async Task<IActionResult> AvatarAsync(string userName)
        {
            try
            {
                var user = await UserService.GetAsync(userName);

                if (user == null)
                    return NotFound();

                var media = await MediaService.FirstOrDefaultAsync(m => m.Type == SDK.Enums.MediaType.Avatar && m.UserId == user.Id);

                if (media == null)
                    return NotFound();

                var fs = System.IO.File.OpenRead(MediaService.GetMediaPath(media));

                return File(fs, media.MimeType);
            }
            catch (Exception ex)
            {
                return NotFound();
            }
        }

        [HttpGet("CustomField/{name}")]
        public async Task<IActionResult> GetCustomFieldAsync(string name)
        {
            try
            {
                var user = await UserService.GetAsync(User?.Identity?.Name);

                var field = await UserCustomFieldService.GetAsync(user.Id, name);

                return Ok(field.Value);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not get the custom field with the name {CustomFieldName}", name);

                return NotFound();
            }
        }

        [HttpPut("CustomField/{name}")]
        public async Task<IActionResult> UpdateCustomFieldAsync(string name, string value)
        {
            try
            {
                var user = await UserService.GetAsync(User?.Identity?.Name);

                await UserCustomFieldService.UpdateAsync(user.Id, name, value);

                return Ok(value);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not update the custom field with the name {CustomFieldName}", name);

                return BadRequest();
            }
        }
    }
}
