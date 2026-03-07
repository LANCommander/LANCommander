namespace LANCommander.Server.UI.Tests;

/// <summary>
/// xUnit collection definition that shares a single ConfiguredServerFixture across all
/// test classes in the "Server" collection. This avoids creating multiple WebApplicationFactory
/// instances that fight over the static DatabaseContext.Provider.
/// </summary>
[CollectionDefinition("Server")]
public class ServerCollection : ICollectionFixture<ConfiguredServerFixture>
{
}

/// <summary>
/// Separate collection for FirstTimeSetupTests which needs its own unconfigured server.
/// Having it in its own collection ensures it doesn't share state with the Server collection.
/// </summary>
[CollectionDefinition("FirstTimeSetup")]
public class FirstTimeSetupCollection : ICollectionFixture<FreshServerFixture>
{
}
