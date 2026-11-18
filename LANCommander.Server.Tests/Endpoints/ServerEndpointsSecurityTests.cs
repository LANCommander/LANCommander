using System.Net;
using System.Net.Http.Headers;
using LANCommander.Server.Controllers;
using LANCommander.Server.Data.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ServerModel = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// Game servers are run and watched by administrators only: <c>/api/Server</c> and the game server hub
/// reveal how servers are run and control them. A server's HTTP paths stay open to players, but only
/// for what is inside their folders.
/// </summary>
[Collection("Application")]
public class ServerEndpointsSecurityTests : BaseTest, IDisposable
{
    private readonly HttpClient _client;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-http-paths-{Guid.NewGuid():N}");

    public ServerEndpointsSecurityTests(ApplicationFixture fixture) : base(fixture)
    {
        // Rejections go through the Identity cookie, which redirects; seeing where is how these tests
        // tell "challenged" from "denied"
        _client = ApplicationFixture.Instance.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    void IDisposable.Dispose()
    {
        Dispose();
        _client.Dispose();

        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private async Task<string> TokenForAsync(User user)
    {
        var (_, secret) = await GetService<PersonalAccessTokenService>().CreateAsync(user.Id, "Test", PersonalAccessTokenScopes.FullApi, null);

        return secret;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token = null)
    {
        // A well-formed body, so what refuses the request is authorization rather than binding
        var request = new HttpRequestMessage(method, path);

        if (method == HttpMethod.Post)
            request.Content = System.Net.Http.Json.JsonContent.Create(new { Text = "status" });

        if (token != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await _client.SendAsync(request);
    }

    private static void ShouldBeRefused(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeFalse();

        if (response.StatusCode == HttpStatusCode.Redirect)
            response.Headers.Location!.ToString().ShouldMatch("(Login|AccessDenied)");
        else
            response.StatusCode.ShouldBeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    public static TheoryData<string, string> ServerRoutes => new()
    {
        { "GET", "/api/Server/" },
        { "GET", "/api/Server/Status" },
        { "GET", $"/api/Server/{Guid.Empty}" },
        { "GET", $"/api/Server/{Guid.Empty}/Status" },
        { "POST", $"/api/Server/{Guid.Empty}/Start" },
        { "POST", $"/api/Server/{Guid.Empty}/Stop" },
        { "GET", $"/api/Server/{Guid.Empty}/Log" },
        { "GET", $"/api/Server/{Guid.Empty}/ProcessInfo" },
        { "POST", $"/api/Server/{Guid.Empty}/Command" },
    };

    [Theory]
    [MemberData(nameof(ServerRoutes))]
    public async Task AnonymousCallersAreRefused(string method, string path)
    {
        using var response = await SendAsync(new HttpMethod(method), path);

        ShouldBeRefused(response);
    }

    [Theory]
    [MemberData(nameof(ServerRoutes))]
    public async Task UsersWhoArentAdministratorsAreRefused(string method, string path)
    {
        var user = await GetService<UserService>().AddAsync(new User { UserName = $"player-{Guid.NewGuid():N}", Approved = true });

        using var response = await SendAsync(new HttpMethod(method), path, await TokenForAsync(user));

        ShouldBeRefused(response);
    }

    [Fact]
    public async Task AdministratorsCanListServers()
    {
        var admin = await EnsureAdminUserCreatedAsync();

        using var response = await SendAsync(HttpMethod.Get, "/api/Server/Status", await TokenForAsync(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TheGameServerHubRefusesAnonymousCallers()
    {
        using var response = await SendAsync(HttpMethod.Post, "/hubs/gameserver/negotiate?negotiateVersion=1");

        ShouldBeRefused(response);
    }

    private async Task<Guid> AddServerServingAsync(string folder)
    {
        var server = await GetService<ServerService>().AddAsync(new ServerModel { Name = $"Http-{Guid.NewGuid():N}" });

        await GetService<ServerHttpPathService>().AddAsync(new ServerHttpPath { ServerId = server.Id, LocalPath = folder, Path = "/" });

        return server.Id;
    }

    [Fact]
    public async Task HttpPathsServeWhatIsInsideTheirFolder()
    {
        var served = Directory.CreateDirectory(Path.Combine(_root, "served")).FullName;
        await File.WriteAllTextAsync(Path.Combine(served, "maps.txt"), "de_dust2");

        var serverId = await AddServerServingAsync(served);

        using var response = await SendAsync(HttpMethod.Get, $"/Server/{serverId}/maps.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("de_dust2");
    }

    [Theory]
    [InlineData("..%5Csecret.txt")]
    [InlineData("..%5C..%5C{0}%5Csecret.txt")]
    [InlineData("%2E%2E%5Csecret.txt")]
    [InlineData("sub%5C..%5C..%5Csecret.txt")]
    public async Task HttpPathsDontReachOutsideTheirFolder(string escape)
    {
        var served = Directory.CreateDirectory(Path.Combine(_root, "served")).FullName;
        Directory.CreateDirectory(Path.Combine(served, "sub"));
        await File.WriteAllTextAsync(Path.Combine(_root, "secret.txt"), "hunter2");

        var serverId = await AddServerServingAsync(served);

        using var response = await SendAsync(HttpMethod.Get, $"/Server/{serverId}/{string.Format(escape, Path.GetFileName(_root))}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("hunter2");
    }

    [Theory]
    [InlineData(@"C:\Games\Served", @"C:\Games\Served", true)]
    [InlineData(@"C:\Games\Served", @"C:\Games\Served\maps\a.txt", true)]
    [InlineData(@"C:\Games\Served\", @"C:\Games\Served\a.txt", true)]
    [InlineData(@"C:\Games\Served", @"C:\Games\Served2\a.txt", false)]
    [InlineData(@"C:\Games\Served", @"C:\Games\secret.txt", false)]
    public void IsWithinOnlyAcceptsTheFolderAndWhatIsInIt(string root, string path, bool expected)
    {
        if (!OperatingSystem.IsWindows())
        {
            root = root.Replace('\\', '/').Replace("C:", "");
            path = path.Replace('\\', '/').Replace("C:", "");
        }

        ServerController.IsWithin(root, path).ShouldBe(expected);
    }
}
