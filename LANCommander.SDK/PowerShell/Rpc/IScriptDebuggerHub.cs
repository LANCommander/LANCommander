using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Rpc;

public interface IScriptDebuggerHub
{
    Task DebugPackageScript(Guid gameId);
    Task SendInput(Guid sessionId, string input);
}