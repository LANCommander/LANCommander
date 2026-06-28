using LANCommander.SDK.Extensions;
using Shouldly;

namespace LANCommander.Server.Tests.Client;

[Collection("Application")]
public class AuthenticationTests : IClassFixture<ApplicationFixture>
{
    private readonly ApplicationFixture _fixture;

    public AuthenticationTests(ApplicationFixture fixture)
    {
        _fixture = ApplicationFixture.Instance;
    }

    [Fact]
    public async Task PingShouldWork()
    {
        // ConnectionClient.PingAsync uses a static HttpClient that does real network I/O and so
        // cannot reach the in-memory test server. Exercise the server's PingMiddleware directly
        // through the in-memory handler instead, asserting the same X-Ping/X-Pong contract.
        var pingId = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Head, _fixture.ServerAddress);
        request.Headers.Add("X-Ping", pingId);

        var response = await _fixture.HttpClient.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.Contains("X-Pong").ShouldBeTrue();
        response.Headers.GetValues("X-Pong").First().ShouldBe(pingId.FastReverse());
    }
}