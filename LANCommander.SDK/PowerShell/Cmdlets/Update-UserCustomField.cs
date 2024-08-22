using PeanutButter.INI;
using System;
using System.IO;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Update, "UserCustomField")]
    [OutputType(typeof(string))]
    public class UpdateUserCustomFieldCmdlet : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string Value { get; set; }

        protected override void ProcessRecord()
        {
            var result = Client.Profile.UpdateCustomField(Name, Value).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
