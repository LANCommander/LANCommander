using System;
using System.Management.Automation;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Enums;
using Svrooij.PowerShell.DI;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Connect, "SteamCmd")]
[OutputType(typeof(SteamCmdStatus))]
[GenerateBindings]
public partial class ConnectSteamCmdCmdlet : DependencyCmdlet<PowerShellStartup>
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Username { get; set; } = string.Empty;

    [Parameter(Mandatory = false)]
    public SecureString? Password { get; set; }

    [ServiceDependency]
    private ISteamCmdService _steamCmdService;

    public override async Task ProcessRecordAsync(CancellationToken token)
    {
        if (_steamCmdService == null)
        {
            WriteError(new ErrorRecord(
                new InvalidOperationException("SteamCmdService is not available in the PowerShell session"),
                "SteamCmdServiceNotAvailable",
                ErrorCategory.InvalidOperation,
                null));
            return;
        }
        
        try
        {
            string? password = null;
            if (Password != null)
            {
                var ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(Password);
                try
                {
                    password = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
                }
                finally
                {
                    System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
                }
            }

            var status = await _steamCmdService.LoginToSteamAsync(Username, password);
            
            WriteObject(status);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "LoginError", ErrorCategory.OperationStopped, null));
        }
    }
}
