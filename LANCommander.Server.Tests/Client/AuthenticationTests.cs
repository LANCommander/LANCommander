using Microsoft.AspNetCore.Mvc.Testing;
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
        var response = await _fixture.Client.PingAsync();
        
        response.ShouldBeTrue();
    }
}