using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LANCommander.SDK.Models;
using Microsoft.IdentityModel.Tokens;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace LANCommander.Server.Services
{
    public class AuthenticationService(ILogger<AuthenticationService> logger, UserService userService) : BaseService(logger)
    {
        public async Task<AuthToken> LoginAsync(string userName, string password)
        {
            if (!String.IsNullOrWhiteSpace(userName) && await userService.CheckPassword(userName, password))
            {
                return await LoginAsync(userName);
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

            if (_settings.Authentication.RequireApproval && !user.Approved && !await userService.IsInRoleAsync(user.UserName, RoleService.AdministratorRoleName))
                throw new Exception("Account must be approved by an administrator");
                
            var userRoles = await userService.GetRolesAsync(user.UserName);

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
