using Microsoft.AspNetCore.Mvc.Testing;

namespace LANCommander.Server.Tests;

public class ApplicationFixture : ApplicationFactory<Program>
{
    public static ApplicationFixture Instance;
    
    public SDK.Client Client { get; set; }
    public IServiceProvider ServiceProvider { get; set; }

    public ApplicationFixture(ApplicationFactory<Program> factory)
    {
        if (Instance != null)
            return;

        //Client = new SDK.Client(factory.CreateClient(), "C:\\Games");
        ServiceProvider = factory.Services;

        Instance = this;
    }
}

[CollectionDefinition("Application")]
public class ApplicationCollection : ICollectionFixture<ApplicationFactory<Program>>
{
    
}