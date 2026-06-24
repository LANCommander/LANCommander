using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Update, "UserCustomField")]
    [OutputType(typeof(string))]
    public class UpdateUserCustomFieldCmdlet(ProfileClient profileClient) : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The name of the custom field to update.")]
        public string Name { get; set; }

        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The new value to set for the custom field.")]
        public string Value { get; set; }

        protected override void ProcessRecord()
        {
            var result = profileClient.UpdateCustomFieldAsync(Name, Value).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
