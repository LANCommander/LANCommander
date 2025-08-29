using System;
using LANCommander.SDK.Attributes;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.SDK.Extensions;

public static partial class HubConnectionExtensions
{
    [HubClientProxy]
    public static partial IDisposable ClientRegistration<T>(this HubConnection connection, T provider);
    
    [HubServerProxy]
    public static partial T ServerProxy<T>(this HubConnection connection);
}