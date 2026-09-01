using System.IdentityModel.Tokens.Jwt;
using LANCommander.HQ.SDK.Authentication;
using LANCommander.Server.Services.HQ;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

public class SettingsHqTokenStoreTests : IDisposable
{
    // Its own settings file per test instance. The default path is process-wide, and SaveAsync now
    // writes synchronously, so sharing it would have parallel test classes fighting over the file.
    private readonly string _settingsPath = Path.Combine(
        Path.GetTempPath(), $"lc-hq-store-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);
    }

    private sealed class StubOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;

        public T Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private (SettingsHqTokenStore Store, ServerSettings Settings) Create()
    {
        var settings = new ServerSettings();

        var store = new SettingsHqTokenStore(
            new SettingsProvider<ServerSettings>(
                new StubOptionsMonitor<ServerSettings>(settings), _settingsPath),
            NullLogger<SettingsHqTokenStore>.Instance);

        return (store, settings);
    }

    // ── Load ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AnEmptyStoreLoadsNothing()
    {
        // Null is how the SDK knows to fall back to a configured seed rather than presenting an
        // empty credential.
        var (store, _) = Create();

        (await store.LoadAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task LoadsTheStoredTokenPair()
    {
        var (store, settings) = Create();
        var accessExpiry = DateTime.UtcNow.AddMinutes(30);
        var refreshExpiry = DateTime.UtcNow.AddDays(60);

        settings.Server.HQ.AccessToken = "access";
        settings.Server.HQ.AccessTokenExpiresAt = accessExpiry;
        settings.Server.HQ.RefreshToken = "refresh";
        settings.Server.HQ.RefreshTokenExpiresAt = refreshExpiry;

        var tokens = await store.LoadAsync();

        tokens.ShouldNotBeNull();
        tokens.AccessToken.ShouldBe("access");
        tokens.RefreshToken.ShouldBe("refresh");
        tokens.AccessTokenExpiresAt!.Value.UtcDateTime.ShouldBe(accessExpiry, TimeSpan.FromSeconds(1));
        tokens.RefreshTokenExpiresAt!.Value.UtcDateTime.ShouldBe(refreshExpiry, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task LoadedExpiriesAreUtc()
    {
        // Settings round-trip DateTime through YAML, which loses Kind. Reading it back as anything
        // other than UTC would shift every expiry comparison by the server's offset.
        var (store, settings) = Create();

        settings.Server.HQ.RefreshToken = "refresh";
        settings.Server.HQ.AccessTokenExpiresAt = DateTime.SpecifyKind(
            DateTime.UtcNow.AddMinutes(30), DateTimeKind.Unspecified);

        var tokens = await store.LoadAsync();

        tokens!.AccessTokenExpiresAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task ARefreshTokenAloneIsStillACredential()
    {
        // The normal state after an access token lapses while the server was switched off. The SDK
        // mints a new access token from the refresh token, so this must not read as "no credential".
        var (store, settings) = Create();

        settings.Server.HQ.RefreshToken = "refresh";

        var tokens = await store.LoadAsync();

        tokens.ShouldNotBeNull();
        tokens.RefreshToken.ShouldBe("refresh");
        tokens.AccessToken.ShouldBeNull();
    }

    [Fact]
    public async Task ALegacyAccessTokenGetsItsExpiryReadFromTheJwt()
    {
        // Upgraded installs have a bare access token and no recorded expiry. Deriving it locally
        // stops the SDK assuming an unknown expiry is still good.
        var (store, settings) = Create();
        var expires = DateTime.UtcNow.AddHours(3);

        settings.Server.HQ.AccessToken = new JwtSecurityTokenHandler()
            .WriteToken(new JwtSecurityToken(expires: expires));
        settings.Server.HQ.AccessTokenExpiresAt = null;

        var tokens = await store.LoadAsync();

        tokens!.AccessTokenExpiresAt.ShouldNotBeNull();
        tokens.AccessTokenExpiresAt!.Value.UtcDateTime.ShouldBe(expires, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task BlankTokensLoadAsNullRatherThanEmptyStrings()
    {
        var (store, settings) = Create();

        settings.Server.HQ.RefreshToken = "refresh";
        settings.Server.HQ.AccessToken = "   ";

        var tokens = await store.LoadAsync();

        tokens!.AccessToken.ShouldBeNull();
    }

    // ── Save ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SavingRoundTripsThroughSettings()
    {
        var (store, settings) = Create();

        var tokens = new HQTokenSet(
            "access-1", DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh-1", DateTimeOffset.UtcNow.AddDays(60));

        await store.SaveAsync(tokens);

        settings.Server.HQ.AccessToken.ShouldBe("access-1");
        settings.Server.HQ.RefreshToken.ShouldBe("refresh-1");

        var reloaded = await store.LoadAsync();

        reloaded!.AccessToken.ShouldBe("access-1");
        reloaded.RefreshToken.ShouldBe("refresh-1");
    }

    [Fact]
    public async Task ARotatedRefreshTokenReachesDiskBeforeSaveReturns()
    {
        // The guarantee this store exists for. Refresh tokens are single-use: the SDK saves the
        // successor and then spends it. If SaveAsync only queued a debounced write, a crash in that
        // window would leave the spent token on disk and the live one nowhere, and the connection
        // could not be recovered without an operator re-authenticating.
        var (store, _) = Create();

        await store.SaveAsync(new HQTokenSet(
            "access-2", DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh-rotated", DateTimeOffset.UtcNow.AddDays(60)));

        // No delay: the debounce is a second, so an unflushed write would not be on disk yet.
        File.Exists(_settingsPath).ShouldBeTrue("SaveAsync must not leave the rotated token only in memory.");
        (await File.ReadAllTextAsync(_settingsPath)).ShouldContain("refresh-rotated");
    }

    [Fact]
    public async Task SavingANullAccessTokenStoresEmptyRatherThanThrowing()
    {
        var (store, settings) = Create();

        await store.SaveAsync(new HQTokenSet(null, null, "refresh-only", null));

        settings.Server.HQ.AccessToken.ShouldBeEmpty();
        settings.Server.HQ.AccessTokenExpiresAt.ShouldBeNull();
        settings.Server.HQ.RefreshToken.ShouldBe("refresh-only");
    }

    // ── Clear ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearingRemovesBothTokens()
    {
        // The SDK clears the store before throwing HQAuthenticationException, i.e. once the
        // credential is provably dead. Leaving anything behind would have the server retrying
        // something that can never work.
        var (store, settings) = Create();

        await store.SaveAsync(new HQTokenSet(
            "access", DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh", DateTimeOffset.UtcNow.AddDays(60)));

        await store.ClearAsync();

        settings.Server.HQ.AccessToken.ShouldBeEmpty();
        settings.Server.HQ.AccessTokenExpiresAt.ShouldBeNull();
        settings.Server.HQ.RefreshToken.ShouldBeEmpty();
        settings.Server.HQ.RefreshTokenExpiresAt.ShouldBeNull();
        settings.Server.HQ.HasCredential.ShouldBeFalse();

        (await store.LoadAsync()).ShouldBeNull();
    }
}
