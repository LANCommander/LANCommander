using Microsoft.AspNetCore.Mvc.Testing;

namespace LANCommander.Server.Tests;

public class ApplicationFixture : WebApplicationFactory<Program>
{
    public static ApplicationFixture Instance;
    
    public SDK.Client Client { get; set; }

    public ApplicationFixture(WebApplicationFactory<Program> factory)
    {
        if (Instance != null)
            return;

        Client = new SDK.Client(factory.CreateClient(), "C:\\Games");

        Instance = this;
    }
}

[CollectionDefinition("Application")]
public class ApplicationCollection : ICollectionFixture<WebApplicationFactory<Program>>
{
    
}