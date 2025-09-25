using System.Management.Automation;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Update, "UserCustomField")]
    [OutputType(typeof(string))]
    public class UpdateUserCustomFieldCmdlet(ProfileClient profileClient) : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Name { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string Value { get; set; }

        protected override void ProcessRecord()
        {
            var result = profileClient.UpdateCustomFieldAsync(Name, Value).GetAwaiter().GetResult();

            WriteObject(result);
        }
    }
}
