using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace LANCommander.Server.Tests.Client;

public class AuthenticationTests :
    IClassFixture<WebApplicationFactory<Program>>,
    IClassFixture<ClientFixture>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SDK.Client _client;

    public AuthenticationTests(
        WebApplicationFactory<Program> factory,
        ClientFixture clientFixture)
    {
        _factory = factory;
        _client = clientFixture.Client;
    }
    
    [Theory]
    [InlineData("http://localhost:1337")]  // Good users
    [InlineData("https://localhost:1337")] // Uninformed users
    [InlineData("localhost")]              // Lazy users
    [InlineData("localhost:1337")]         // Forgivable users
    [InlineData("localhost:1338")]         // PEBKAC users
    public async Task CanReachServer(string serverAddress)
    {
        var app = _factory.CreateClient();

        await _client.ChangeServerAddressAsync(serverAddress);
        
        _client.GetServerAddress().ShouldBe("http://localhost:1337");
    }
}