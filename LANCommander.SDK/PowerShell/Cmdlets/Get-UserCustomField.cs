using PeanutButter.INI;
using System;
using System.IO;
using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "UserCustomField")]
    [OutputType(typeof(string))]
    public class GetUserCustomFieldCmdlet(ProfileClient profileClient) : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var result = profileClient.GetCustomFieldAsync(Name).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
