using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Rpc.Client;

public partial interface IRpcClient
{
    Task Log_ConsoleOutputAsync(Guid sessionId, string content);
}