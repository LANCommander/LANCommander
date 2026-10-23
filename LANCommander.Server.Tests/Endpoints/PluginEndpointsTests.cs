using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Plugins;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// Plugin endpoints mapped on a bare test server, with a stand-in authentication scheme, so each
/// case controls exactly who is calling.
/// </summary>
public class PluginEndpointsTests
{
    [Fact]
    public async Task AnExtensionThatDeclaresNothingIsForAdministratorsOnly()
    {
        await using var server = await StartAsync(new Extension("Locked"));

        (await GetAsync(server, "/api/Plugins/Locked/Ping")).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetAsync(server, "/api/Plugins/Locked/Ping", "alice")).ShouldBe(HttpStatusCode.Forbidden);
        (await GetAsync(server, "/api/Plugins/Locked/Ping", "admin", SDK.Roles.Administrator)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnExtensionCanOpenItsEndpointsToEverySignedInUser()
    {
        await using var server = await StartAsync(new Extension("Open", PluginAccessPolicy.AuthenticatedUser));

        (await GetAsync(server, "/api/Plugins/Open/Ping")).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetAsync(server, "/api/Plugins/Open/Ping", "alice")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnExtensionCanRequireARole()
    {
        await using var server = await StartAsync(new Extension("Curated", PluginAccessPolicy.RequireRoles("Curator")));

        (await GetAsync(server, "/api/Plugins/Curated/Ping", "alice")).ShouldBe(HttpStatusCode.Forbidden);
        (await GetAsync(server, "/api/Plugins/Curated/Ping", "bob", "Curator")).ShouldBe(HttpStatusCode.OK);
        (await GetAsync(server, "/api/Plugins/Curated/Ping", "admin", SDK.Roles.Administrator)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ASingleEndpointCanBeOpenedToAnonymousCallers()
    {
        await using var server = await StartAsync(new Extension("Mixed"));

        (await GetAsync(server, "/api/Plugins/Mixed/Public")).ShouldBe(HttpStatusCode.OK);
        (await GetAsync(server, "/api/Plugins/Mixed/Ping")).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheEndpointSeesTheCaller()
    {
        await using var server = await StartAsync(new Extension("Echo", PluginAccessPolicy.AuthenticatedUser));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Plugins/Echo/WhoAmI");
        request.Headers.Add(TestAuthentication.UserHeader, "alice");

        var response = await server.GetTestClient().SendAsync(request);

        (await response.Content.ReadAsStringAsync()).ShouldBe("alice");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Two/Segments")]
    [InlineData("../Escape")]
    [InlineData("-Leading")]
    [InlineData("With Space")]
    public async Task AnInvalidRoutePrefixIsIgnored(string prefix)
    {
        await using var server = await StartAsync(new Extension(prefix, PluginAccessPolicy.AuthenticatedUser), new Extension("Good", PluginAccessPolicy.AuthenticatedUser));

        (await GetAsync(server, "/api/Plugins/Good/Ping", "alice")).ShouldBe(HttpStatusCode.OK);
        Resolve(new Extension(prefix)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtensionsSharingAPrefixAreAllIgnored()
    {
        await using var server = await StartAsync(
            new Extension("Shared", PluginAccessPolicy.AuthenticatedUser),
            new Extension("shared", PluginAccessPolicy.AuthenticatedUser),
            new Extension("Other", PluginAccessPolicy.AuthenticatedUser));

        (await GetAsync(server, "/api/Plugins/Shared/Ping", "alice")).ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync(server, "/api/Plugins/Other/Ping", "alice")).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnExtensionThatThrowsWhileMappingDoesNotStopTheOthers()
    {
        await using var server = await StartAsync(
            new Extension("Broken", PluginAccessPolicy.AuthenticatedUser, throwWhileMapping: true),
            new Extension("Fine", PluginAccessPolicy.AuthenticatedUser));

        (await GetAsync(server, "/api/Plugins/Fine/Ping", "alice")).ShouldBe(HttpStatusCode.OK);

        // What it mapped before throwing is still behind its policy
        (await GetAsync(server, "/api/Plugins/Broken/Ping")).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void AnExtensionWhoseAccessCannotBeReadIsIgnored() =>
        Resolve(new UnreadableExtension()).ShouldBeEmpty();

    private static IReadOnlyList<(IServerEndpointExtension Extension, PluginAccessPolicy Access)> Resolve(params IServerEndpointExtension[] extensions) =>
        PluginEndpoints.Resolve(extensions, NullLogger.Instance);

    private static async Task<HttpStatusCode> GetAsync(WebApplication server, string path, string? user = null, params string[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (user is not null)
        {
            request.Headers.Add(TestAuthentication.UserHeader, user);

            foreach (var role in roles)
                request.Headers.Add(TestAuthentication.RoleHeader, role);
        }

        return (await server.GetTestClient().SendAsync(request)).StatusCode;
    }

    private static async Task<WebApplication> StartAsync(params IServerEndpointExtension[] extensions)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddRouting();
        builder.Services.AddAuthorization();
        builder.Services
            .AddAuthentication(TestAuthentication.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.Scheme, null);

        foreach (var extension in extensions)
            builder.Services.AddSingleton(extension);

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapPluginEndpoints());

        await app.StartAsync();

        return app;
    }

    private sealed class Extension(string prefix, PluginAccessPolicy? access = null, bool throwWhileMapping = false) : IServerEndpointExtension
    {
        public string RoutePrefix => prefix;

        public PluginAccessPolicy Access => access ?? PluginAccessPolicy.Administrator;

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/Ping", () => "pong");

            if (throwWhileMapping)
                throw new InvalidOperationException("The plugin is broken");

            endpoints.MapGet("/Public", () => "hello").AllowAnonymous();
            endpoints.MapGet("/WhoAmI", (ClaimsPrincipal user) => user.Identity!.Name);
        }
    }

    private sealed class UnreadableExtension : IServerEndpointExtension
    {
        public string RoutePrefix => "Unreadable";

        public PluginAccessPolicy Access => throw new TypeLoadException("A type the plugin needs is missing");

        public void MapEndpoints(IEndpointRouteBuilder endpoints) => throw new InvalidOperationException("Never mapped");
    }

    /// <summary>Signs in whoever the request's headers name, with the roles they list.</summary>
    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";
        public const string UserHeader = "X-Test-User";
        public const string RoleHeader = "X-Test-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };

            claims.AddRange(Request.Headers[RoleHeader].Select(role => new Claim(ClaimTypes.Role, role!)));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));

            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme)));
        }
    }
}
