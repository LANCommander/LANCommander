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
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        protected readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly SignInManager<User> SignInManager;
        private readonly UserManager<User> UserManager;
        private readonly IUserStore<User> UserStore;
        private readonly RoleManager<Role> RoleManager;
        private readonly LANCommanderSettings Settings;

        public AuthController(SignInManager<User> signInManager, UserManager<User> userManager, IUserStore<User> userStore, RoleManager<Role> roleManager)
        {
            SignInManager = signInManager;
            UserManager = userManager;
            UserStore = userStore;
            RoleManager = roleManager;
            Settings = SettingService.GetSettings();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            var user = await UserManager.FindByNameAsync(model.UserName);

            try
            {
                var token = await Login(user, model.Password);

                Logger.Debug("Successfully logged in user {UserName}", user.UserName);

                return Ok(token);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred while trying to log in {UserName}", model.UserName);

                return Unauthorized();
            }
        }

        [HttpPost("Logout")]
        public async Task<IActionResult> Logout()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                await SignInManager.SignOutAsync();

            return Ok();
        }

        [HttpPost("Validate")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public IActionResult Validate()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
                return Ok();
            else
                return Unauthorized();
        } 

        [HttpPost("Refresh")]
        public async Task<IActionResult> Refresh(TokenModel token)
        {
            if (token == null)
            {
                Logger.Debug("Null token passed when trying to refresh");
                return BadRequest("Invalid client request");
            }

            var principal = GetPrincipalFromExpiredToken(token.AccessToken);

            if (principal == null)
            {
                Logger.Debug("Invalid access token or refresh token");
                return BadRequest("Invalid access token or refresh token");
            }

            var user = await UserManager.FindByNameAsync(principal.Identity.Name);

            if (user == null || user.RefreshToken != token.RefreshToken || user.RefreshTokenExpiration <= DateTime.Now)
            {
                Logger.Debug("Invalid access token or refresh token for user {UserName}", principal.Identity.Name);
                return BadRequest("Invalid access token or refresh token");
            }

            var newAccessToken = GetToken(principal.Claims.ToList());
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;

            await UserManager.UpdateAsync(user);

            Logger.Debug("Successfully refreshed token for user {UserName}", user.UserName);

            return Ok(new
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(newAccessToken),
                RefreshToken = newRefreshToken,
                Expiration = newAccessToken.ValidTo
            });
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            var user = await UserManager.FindByNameAsync(model.UserName);

            if (user != null)
            {
                Logger.Debug("Cannot register user with username {UserName}, already exists", model.UserName);

                return Unauthorized(new
                {
                    Message = "Username is unavailable"
                });
            }

            user = new User();

            await UserStore.SetUserNameAsync(user, model.UserName, CancellationToken.None);

            var result = await UserManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                try
                {
                    if (Settings.Roles.DefaultRoleId != Guid.Empty)
                    {
                        var defaultRole = await RoleManager.FindByIdAsync(Settings.Roles.DefaultRoleId.ToString());

                        if (defaultRole != null)
                            await UserManager.AddToRoleAsync(user, defaultRole.Name);
                    }

                    var token = await Login(user, model.Password);

                    Logger.Debug("Successfully registered user {UserName}", user.UserName);

                    return Ok(token);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Could not register user {UserName}", user.UserName);
                    return BadRequest(new
                    {
                        Message = "An unknown error occurred"
                    });
                }
            }

            return Unauthorized(new
            {
                Message = "Error:\n" + String.Join('\n', result.Errors.Select(e => e.Description))
            });
        }

        private async Task<TokenModel> Login(User user, string password)
        {
            if (user != null && await UserManager.CheckPasswordAsync(user, password))
            {
                Logger.Debug("Password check for user {UserName} was successful", user.UserName);

                if (Settings.Authentication.RequireApproval && !user.Approved && (!await UserManager.IsInRoleAsync(user, "Administrator")))
                    throw new Exception("Account must be approved by an administrator");

                var userRoles = await UserManager.GetRolesAsync(user);

                var authClaims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.UserName),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                };

                foreach (var userRole in userRoles)
                {
                    authClaims.Add(new Claim(ClaimTypes.Role, userRole));
                }

                Logger.Debug("Generating authentication token for user {UserName}", user.UserName);

                var token = GetToken(authClaims);
                var refreshToken = GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiration = DateTime.Now.AddDays(Settings.Authentication.TokenLifetime);

                await UserManager.UpdateAsync(user);

                return new TokenModel()
                {
                    AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                    RefreshToken = refreshToken,
                    Expiration = token.ValidTo
                };
            }

            throw new Exception("Invalid username or password");
        }

        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Authentication.TokenSecret));

            var token = new JwtSecurityToken(
                expires: DateTime.Now.AddDays(Settings.Authentication.TokenLifetime),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }

        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];

            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);

                return Convert.ToBase64String(randomNumber);
            }
        }

        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Settings.Authentication.TokenSecret)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
        }
    }
}
