using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell;

public interface IScriptDebugContext
{
    Guid SessionId { get; set; }
    Task ExecuteAsync(string script);
}