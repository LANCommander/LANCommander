namespace LANCommander.Server.Tests;

public class ClientFixture : IDisposable
{
    public SDK.Client Client { get; set; }
    
    public ClientFixture()
    {
        Client = new SDK.Client("", "C:\\Games");
    }

    public void Dispose()
    {
        Client.Disconnect();
    }
}