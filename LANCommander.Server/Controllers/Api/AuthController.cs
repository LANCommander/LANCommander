using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using LANCommander.SDK.Models;
using Microsoft.AspNetCore.Authentication;
using ZiggyCreatures.Caching.Fusion;
using AuthenticationService = LANCommander.Server.Services.AuthenticationService;
using User = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Controllers.Api
{
    public class TokenModel
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime Expiration { get; set; }
    }

    public class LoginModel
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class RegisterModel
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string PasswordConfirmation { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : BaseApiController
    {
        private readonly AuthenticationService AuthenticationService;
        private readonly UserService UserService;
        private readonly RoleService RoleService;
        private readonly IFusionCache Cache;
        private readonly IMapper Mapper;

        public AuthController(
            AuthenticationService authenticationService,
            UserService userService,
            RoleService roleService,
            IFusionCache cache,
            IMapper mapper,
            ILogger<AuthController> logger) : base(logger)
        {
            AuthenticationService = authenticationService;
            UserService = userService;
            RoleService = roleService;
            Cache = cache;
            Mapper = mapper;
        }

        [HttpGet("Login")]
        public async Task<IActionResult> LoginAsync(string provider = "")
        {
            if (!String.IsNullOrWhiteSpace(provider) && !User.Identity.IsAuthenticated)
            {
                var properties = new AuthenticationProperties(new Dictionary<string, string>()
                {
                    { "Action", AuthenticationProviderActionType.Login }
                });
                
                properties.RedirectUri = Url.Action("Login", "Auth", new { provider = provider });
                
                return Challenge(properties, provider);
            }
            else if (!String.IsNullOrWhiteSpace(provider) && User.Identity.IsAuthenticated)
            {
                var code = Guid.NewGuid().ToString();
                var token = await AuthenticationService.LoginAsync(User.Identity.Name);
                
                await Cache.SetAsync($"AuthToken/{code}", token, TimeSpan.FromMinutes(5));

                return Redirect($"/RedeemToken/{code}");
            }
            else
            {
                return BadRequest();
            }
        }

        [HttpPost("Login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginModel model)
        {
            try
            {
                var token = await AuthenticationService.LoginAsync(model.UserName, model.Password);

                Logger?.LogDebug("Successfully logged in user {UserName}", model.UserName);

                return Ok(token);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "An error occurred while trying to log in {UserName}", model.UserName);

                return Unauthorized();
            }
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> LogoutAsync()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                await UserService.SignOut();

            Logger?.LogInformation("Logged out user {UserName}", User.Identity.Name);

            return Ok();
        }

        [HttpPost("Validate")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public IActionResult ValidateAsync()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                return Ok();
            else
                return Unauthorized();
        } 

        [HttpPost("Refresh")]
        public async Task<IActionResult> RefreshAsync(AuthToken token)
        {
            try
            {
                return Ok(await AuthenticationService.RefreshTokenAsync(token));
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            } ;
        }

        [HttpPost("Register")]
        public async Task<IActionResult> RegisterAsync([FromBody] RegisterModel model)
        {
            if (model.Password != model.PasswordConfirmation)
                return Unauthorized(new
                {
                    Message = "Passwords don't match."
                });
            
            var user = await UserService.GetAsync(model.UserName);

            if (user != null)
            {
                Logger?.LogDebug("Cannot register user with username {UserName}, already exists", model.UserName);

                return Unauthorized(new
                {
                    Message = "Username is unavailable"
                });
            }

            user = new User();

            user.UserName = model.UserName;

            user = await UserService.AddAsync(user);

            if (user != null)
            {
                await UserService.ChangePassword(user.UserName, model.Password);

                try
                {
                    if (Settings.Roles.DefaultRoleId != Guid.Empty)
                    {
                        var defaultRole = await RoleService.GetAsync(Settings.Roles.DefaultRoleId);

                        if (defaultRole != null)
                            await UserService.AddToRoleAsync(user.UserName, defaultRole.Name);
                    }

                    var token = await AuthenticationService.LoginAsync(user.UserName, model.Password);

                    Logger?.LogDebug("Successfully registered user {UserName}", user.UserName);

                    return Ok(token);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not register user {UserName}", user.UserName);
                    return BadRequest(new
                    {
                        Message = "An unknown error occurred"
                    });
                }
            }

            return Unauthorized(new
            {
                //Message = "Error:\n" + String.Join('\n', result.Errors.Select(e => e.Description))
            });
        }

        [HttpGet("AuthenticationProviders")]
        public IActionResult GetAuthenticationProviders()
        {
            return Ok(Mapper.Map<IEnumerable<AuthenticationProvider>>(Settings.Authentication.AuthenticationProviders));
        }
    }
}
