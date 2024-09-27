using PeanutButter.INI;
using System;
using System.IO;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "UserCustomField")]
    [OutputType(typeof(string))]
    public class GetUserCustomFieldCmdlet : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var result = Client.Profile.GetCustomField(Name).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
