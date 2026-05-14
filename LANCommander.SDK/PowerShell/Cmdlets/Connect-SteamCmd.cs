using System;
using System.Management.Automation;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Enums;

namespace LANCommander.SDK.PowerShell.Cmdlets;

[Cmdlet(VerbsCommunications.Connect, "SteamCmd", HelpUri = "https://lancommander.app/Scripting/Cmdlets")]
[OutputType(typeof(SteamCmdStatus))]
public class ConnectSteamCmdCmdlet : AsyncCmdlet
{
    [Parameter(Mandatory = true, Position = 0, HelpMessage = "The Steam account username to log in with.")]
    public string Username { get; set; } = string.Empty;

    [Parameter(Mandatory = false, HelpMessage = "The password for the Steam account. If omitted, an anonymous login is attempted.")]
    public SecureString? Password { get; set; }

    protected override async Task ProcessRecordAsync(CancellationToken token)
    {
        var steamCmdService = SteamServicesProvider.GetSteamCmdService(SessionState);

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

            var status = await steamCmdService.LoginToSteamAsync(Username, password);
            
            WriteObject(status);
        }
        catch (Exception ex)
        {
            WriteError(new ErrorRecord(ex, "LoginError", ErrorCategory.OperationStopped, null));
        }
    }
}
