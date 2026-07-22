using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "Runtime")]
    [OutputType(typeof(RuntimePlatform))]
    public class GetRuntimeCmdlet : Cmdlet
    {
        protected override void ProcessRecord()
        {
            WriteObject(EnvironmentHelper.GetCurrentRuntime());
        }
    }
}
