using System;

namespace LANCommander.SDK.Abstractions;

public interface IServerAddressProvider
{
    void SetServerAddress(Uri serverAddress);
    Uri GetServerAddress();
}