using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Client;

/// <summary>
/// Personal access tokens against the full server pipeline. Each request carries its own header,
/// so none of these touch the fixture's shared SDK token.
/// </summary>
[Collection("Application")]
public class PersonalAccessTokenTests : IClassFixture<ApplicationFixture>
{
    private readonly ApplicationFixture _fixture;
    private readonly HttpClient _client;

    public PersonalAccessTokenTests(ApplicationFixture fixture, ApplicationFactory<Program> factory)
    {
        _fixture = ApplicationFixture.Instance;

        // Unauthenticated API calls are challenged by the cookie scheme; seeing that redirect is
        // how these tests tell "rejected" from "the login page rendered fine"
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task ATokenAuthenticatesAsItsOwner()
    {
        var user = await CreateUserAsync();
        var (_, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        using var response = await SendAsync(HttpMethod.Get, "/api/Profile", secret);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SDK.Models.User>())!.UserName.ShouldBe(user.UserName);
    }

    [Fact]
    public async Task OnlyAHashOfTheTokenIsStored()
    {
        var user = await CreateUserAsync();
        var (token, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        secret.ShouldStartWith(PersonalAccessTokenService.TokenPrefix);
        token.TokenHash.ShouldNotContain(secret[PersonalAccessTokenService.TokenPrefix.Length..]);
        secret.ShouldStartWith(token.TokenPrefix);
    }

    [Fact]
    public async Task AnUnknownTokenIsRejected()
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", PersonalAccessTokenService.TokenPrefix + "not-a-real-token");

        ShouldBeChallenged(response);
    }

    [Fact]
    public async Task ARevokedTokenStopsWorking()
    {
        var user = await CreateUserAsync();
        var (token, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        using (var before = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", secret))
            before.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<PersonalAccessTokenService>().RevokeAsync(user.Id, token.Id)).ShouldBeTrue();

        using var after = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", secret);
        ShouldBeChallenged(after);
    }

    [Fact]
    public async Task AUserCannotRevokeSomeoneElsesToken()
    {
        var owner = await CreateUserAsync();
        var other = await CreateUserAsync();
        var (token, secret) = await CreateTokenAsync(owner, PersonalAccessTokenScopes.FullApi);

        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<PersonalAccessTokenService>().RevokeAsync(other.Id, token.Id)).ShouldBeFalse();

        using var response = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", secret);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        var user = await CreateUserAsync();
        var (token, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi, DateTime.UtcNow.AddDays(1));

        await UpdateTokenAsync(token.Id, t => t.ExpiresOn = DateTime.UtcNow.AddMinutes(-1));

        using var response = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", secret);
        ShouldBeChallenged(response);
    }

    [Fact]
    public async Task ATokenCannotBeIssuedAlreadyExpired()
    {
        var user = await CreateUserAsync();

        await Should.ThrowAsync<ArgumentException>(() =>
            CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi, DateTime.UtcNow.AddMinutes(-1)));
    }

    [Fact]
    public async Task APluginScopedTokenIsForbiddenEverywhereElse()
    {
        var user = await CreateUserAsync();
        var (_, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.ForPlugins(["Mcp"]));

        using (var profile = await SendAsync(HttpMethod.Get, "/api/Profile", secret))
            profile.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using (var otherPlugin = await SendAsync(HttpMethod.Get, "/api/Plugins/Other/Ping", secret))
            otherPlugin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // No plugin maps /api/Plugins/Mcp in tests, so the request falls through to the UI's
        // not-found page; getting that far is what shows the token was accepted
        using var own = await SendAsync(HttpMethod.Get, "/api/Plugins/Mcp/Ping", secret);
        own.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        own.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task RolesAreReadFromTheAccountOnEveryRequest()
    {
        var user = await CreateUserAsync();
        var (_, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        using (var before = await SendAsync(HttpMethod.Get, "/api/Issue", secret))
            ShouldBeDenied(before);

        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        if (await roleService.GetAsync(RoleService.AdministratorRoleName) == null)
            await roleService.AddAsync(new Role { Name = RoleService.AdministratorRoleName });

        await userService.AddToRoleAsync(user.UserName!, RoleService.AdministratorRoleName);

        using (var promoted = await SendAsync(HttpMethod.Get, "/api/Issue", secret))
            promoted.StatusCode.ShouldBe(HttpStatusCode.OK);

        await userService.RemoveFromRole(user.UserName!, RoleService.AdministratorRoleName);

        using var demoted = await SendAsync(HttpMethod.Get, "/api/Issue", secret);
        ShouldBeDenied(demoted);
    }

    [Fact]
    public async Task UseIsRecorded()
    {
        var user = await CreateUserAsync();
        var (token, secret) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        token.LastUsedOn.ShouldBeNull();

        using (var response = await SendAsync(HttpMethod.Post, "/api/Auth/Validate", secret))
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<PersonalAccessTokenService>().GetAsync(token.Id);

        stored.LastUsedOn.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeletingTheUserDeletesTheirTokens()
    {
        var user = await CreateUserAsync();
        var (token, _) = await CreateTokenAsync(user, PersonalAccessTokenScopes.FullApi);

        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            // Cascades are applied by the change tracker on InMemory, so the tokens must be loaded
            var stored = await context.Users!.Include(u => u.PersonalAccessTokens).FirstAsync(u => u.Id == user.Id);

            context.Users!.Remove(stored);
            await context.SaveChangesAsync();
        }

        await using (var context = await contextFactory.CreateDbContextAsync())
            (await context.PersonalAccessTokens!.AnyAsync(t => t.Id == token.Id)).ShouldBeFalse();
    }

    // The server's default challenge and forbid go through the Identity cookie, which redirects
    private static void ShouldBeChallenged(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/Login");
    }

    private static void ShouldBeDenied(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("/AccessDenied");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string secret)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

        return await _client.SendAsync(request);
    }

    private async Task<User> CreateUserAsync()
    {
        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<UserService>().AddAsync(new User
        {
            UserName = $"pat-test-{Guid.NewGuid():N}",
            Approved = true,
        });
    }

    private async Task<(PersonalAccessToken Token, string Secret)> CreateTokenAsync(User user, PersonalAccessTokenScopes scopes, DateTime? expiresOn = null)
    {
        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<PersonalAccessTokenService>()
            .CreateAsync(user.Id, "Test", scopes, expiresOn);
    }

    private async Task UpdateTokenAsync(Guid tokenId, Action<PersonalAccessToken> update)
    {
        var contextFactory = _fixture.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        update(await context.PersonalAccessTokens!.FirstAsync(t => t.Id == tokenId));

        await context.SaveChangesAsync();
    }
}
