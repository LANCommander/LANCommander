using System.Security.Cryptography;
using System.Text;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class PersonalAccessTokenService(
        ILogger<PersonalAccessTokenService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IFusionCache cache,
        IHttpContextAccessor httpContextAccessor,
        IDbContextFactory<DatabaseContext> contextFactory) : BaseDatabaseService<PersonalAccessToken>(logger, settingsProvider, cache, httpContextAccessor, contextFactory)
    {
        /// <summary>
        /// Starts every token, so the server can tell one from a JWT without parsing it and secret
        /// scanners can recognise a leaked one.
        /// </summary>
        public const string TokenPrefix = "lc_pat_";

        // How many characters of the token, after the prefix, are kept for display
        private const int DisplayLength = 6;

        // Last-used times are informational; writing one on every request would turn reads into writes
        private static readonly TimeSpan LastUsedResolution = TimeSpan.FromMinutes(1);

        public override async Task<PersonalAccessToken> AddAsync(PersonalAccessToken entity)
        {
            return await base.AddAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(t => t.User);
            });
        }

        public override async Task<PersonalAccessToken> UpdateAsync(PersonalAccessToken entity)
        {
            return await base.UpdateAsync(entity, async context =>
            {
                await context.UpdateRelationshipAsync(t => t.User);
            });
        }

        /// <summary>
        /// Issues a token for <paramref name="userId"/>. The returned secret is the only copy of the
        /// plaintext; it can't be recovered later.
        /// </summary>
        public async Task<(PersonalAccessToken Token, string Secret)> CreateAsync(
            Guid userId,
            string name,
            PersonalAccessTokenScopes scopes,
            DateTime? expiresOn)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A token needs a name.", nameof(name));

            if (name.Length > 128)
                throw new ArgumentException("Token names must be 128 characters or shorter.", nameof(name));

            if (expiresOn.HasValue && expiresOn.Value <= DateTime.UtcNow)
                throw new ArgumentException("A token can't expire in the past.", nameof(expiresOn));

            var secret = TokenPrefix + Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

            var token = await AddAsync(new PersonalAccessToken
            {
                UserId = userId,
                Name = name.Trim(),
                TokenHash = Hash(secret),
                TokenPrefix = secret[..(TokenPrefix.Length + DisplayLength)],
                Scopes = scopes.ToString(),
                ExpiresOn = expiresOn,
            });

            return (token, secret);
        }

        /// <summary>
        /// Looks up an unexpired token by its plaintext, with its user loaded, and records that it was
        /// used. Returns null for anything that isn't a live token.
        /// </summary>
        public async Task<PersonalAccessToken?> ValidateAsync(string? secret)
        {
            if (!IsPersonalAccessToken(secret))
                return null;

            var hash = Hash(secret!);

            using var context = await contextFactory.CreateDbContextAsync();

            var token = await context.PersonalAccessTokens!
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (token?.User == null)
                return null;

            var now = DateTime.UtcNow;

            if (token.ExpiresOn.HasValue && token.ExpiresOn.Value <= now)
                return null;

            if (!token.LastUsedOn.HasValue || now - token.LastUsedOn.Value >= LastUsedResolution)
            {
                token.LastUsedOn = now;

                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException ex)
                {
                    // A concurrent revoke, or another request touching the same row; neither should
                    // fail the request that is merely being authenticated
                    _logger?.LogDebug(ex, "Could not record use of personal access token {TokenId}", token.Id);
                }
            }

            return token;
        }

        /// <summary>Revokes one of <paramref name="userId"/>'s tokens. Revoked tokens are deleted.</summary>
        public async Task<bool> RevokeAsync(Guid userId, Guid tokenId)
        {
            var token = await FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId);

            if (token == null)
                return false;

            await DeleteAsync(token);

            return true;
        }

        public static bool IsPersonalAccessToken(string? value) =>
            !string.IsNullOrEmpty(value) && value.StartsWith(TokenPrefix, StringComparison.Ordinal);

        // The token is 256 random bits, so a fast hash is enough: there's nothing to brute-force
        internal static string Hash(string secret) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

        private static string Base64UrlEncode(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
