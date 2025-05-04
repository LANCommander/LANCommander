using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Exceptions;
using Microsoft.IdentityModel.Tokens;
using YamlDotNet.Serialization;
using PascalCaseNamingConvention = YamlDotNet.Serialization.NamingConventions.PascalCaseNamingConvention;

namespace LANCommander.Server.Services
{
    public class AuthenticationService(
        ILogger<AuthenticationService> logger,
        IMapper mapper,
        UserService userService,
        RoleService roleService,
        ScriptService scriptService) : BaseService(logger)
    {
        public async Task<AuthToken> LoginAsync(string userName, string password)
        {
            if (!String.IsNullOrWhiteSpace(userName) && await userService.CheckPassword(userName, password))
            {
                var token = await LoginAsync(userName);

                try
                {
                    var user = await userService.GetAsync<User>(userName);
                    var scripts = await scriptService.GetAsync<SDK.Models.Script>(s => s.Type == ScriptType.UserLogin);

                    if (scripts.Any())
                    {
                        var client = new SDK.Client(_settings.Beacon.Address, "", logger);

                        client.UseToken(token);

                        foreach (var script in scripts)
                        {
                            await client.Scripts.RunUserLoginScript(script, user);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not execute user login script");
                }

                return token;
            }
            else
                throw new Exception("Invalid username or password");
        }

        public async Task<AuthToken> LoginAsync(string userName)
        {
            var user = await userService.GetAsync(userName);
            
            if (user == null)
                throw new Exception("Invalid username or password");
                
            _logger?.LogDebug("Password check for user {UserName} was successful", user.UserName);

            if (_settings.Authentication.RequireApproval && !user.Approved && !await userService.IsInRoleAsync(user, RoleService.AdministratorRoleName))
                throw new Exception("Account must be approved by an administrator");
                
            var userRoles = await userService.GetRolesAsync(user);

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            foreach (var userRole in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, userRole.Name));
            }
                
            _logger?.LogDebug("Generating authentication token for user {UserName}", user.UserName);
                
            var token = GetToken(authClaims);
            var refreshToken = GenerateRefreshToken();
                
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiration = DateTime.UtcNow.AddDays(_settings.Authentication.TokenLifetime);
                
            await userService.UpdateAsync(user);

            return new AuthToken
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken,
                Expiration = token.ValidTo
            };
        }

        public async Task<AuthToken> RefreshTokenAsync(AuthToken token)
        {
            if (token == null)
            {
                _logger?.LogDebug("Refresh token is null");
                
                throw new Exception("Invalid refresh token");
            }
            
            var principal = GetPrincipalFromExpiredToken(token.AccessToken);

            if (principal == null)
            {
                _logger?.LogDebug("Invalid access token or refresh token");
                throw new Exception("Invalid access token or refresh token");
            }
            
            var user = await userService.GetAsync(principal.Identity.Name);

            if (user == null || user.RefreshToken != token.RefreshToken ||
                user.RefreshTokenExpiration <= DateTime.UtcNow)
            {
                _logger?.LogDebug("Refresh token expired");
                throw new Exception("Invalid refresh token");
            }
            
            var newAccessToken = GetToken(principal.Claims.ToList());
            var newRefreshToken = GenerateRefreshToken();
            
            user.RefreshToken = newRefreshToken;
            
            await userService.UpdateAsync(user);
            
            _logger?.LogDebug("Refresh token updated for user {UserName}", user.UserName);

            return new AuthToken
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(newAccessToken),
                RefreshToken = newRefreshToken,
                Expiration = newAccessToken.ValidTo
            };
        }

        public async Task<AuthToken> RegisterAsync(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new UserRegistrationException("Password is empty");

            var user = await userService.GetAsync(userName);

            if (user != null)
            {
                _logger?.LogDebug("Cannot register user with username {UserName}, already exists", userName);

                throw new UserRegistrationException("Username is unavailable");
            }

            user = new Data.Models.User();
            user.UserName = userName;

            user = await userService.AddAsync(user);

            if (user != null)
            {
                await userService.ChangePassword(user.UserName, password);

                try
                {
                    if (_settings.Roles.DefaultRoleId == Guid.Empty)
                    {
                        var defaultRole = await roleService.GetAsync(_settings.Roles.DefaultRoleId);

                        if (defaultRole != null)
                            await userService.AddToRoleAsync(user.UserName, defaultRole.Name);
                    }

                    var token = await LoginAsync(user.UserName, password);

                    logger?.LogDebug("Successfully registered user {UserName}", user.UserName);
                    
                    try
                    {
                        var scripts = await scriptService.GetAsync<SDK.Models.Script>(s => s.Type == ScriptType.UserLogin);

                        if (scripts.Any())
                        {
                            var client = new SDK.Client(_settings.Beacon.Address, "", logger);

                            client.UseToken(token);

                            foreach (var script in scripts)
                            {
                                await client.Scripts.RunUserRegistrationScript(script, mapper.Map<SDK.Models.User>(user));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not execute user login script");
                    }

                    return token;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not register user {UserName}", user.UserName);
                    throw new UserRegistrationException("An unknown error occurred while registering");
                }
            }
            else
                throw new UserRegistrationException("Unknown Error");
        }
        
        private ClaimsPrincipal? GetPrincipalFromExpiredToken(string? token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Authentication.TokenSecret)),
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
            if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                throw new SecurityTokenException("Invalid token");

            return principal;
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
        
        private JwtSecurityToken GetToken(List<Claim> authClaims)
        {
            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Authentication.TokenSecret));

            var token = new JwtSecurityToken(
                expires: DateTime.UtcNow.AddDays(_settings.Authentication.TokenLifetime),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }

        public static async Task<List<Models.AuthenticationProvider>> GetAuthenticationProviderTemplatesAsync()
        {
            var files = Directory.GetFiles(@"Templates/AuthenticationProviders", "*.yml", SearchOption.AllDirectories);

            var externalProviders = new List<Models.AuthenticationProvider>();


            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            foreach (var file in files)
            {
                try
                {
                    var contents = await File.ReadAllTextAsync(file);

                    externalProviders.Add(deserializer.Deserialize<Models.AuthenticationProvider>(contents));
                }
                catch { }
            }

            return externalProviders;
        }
    }
}
