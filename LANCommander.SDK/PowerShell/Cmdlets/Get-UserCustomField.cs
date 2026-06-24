using PeanutButter.INI;
using System;
using System.IO;
using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "UserCustomField")]
    [OutputType(typeof(string))]
    public class GetUserCustomFieldCmdlet(ProfileClient profileClient) : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the custom field to retrieve for the current user.")]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            var result = profileClient.GetCustomFieldAsync(Name).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
