using System.Management.Automation;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.PowerShell.Cmdlets
{

    [Cmdlet(VerbsCommunications.Write, "ReplaceContentInFile")]
    [OutputType(typeof(string))]
    public class ReplaceContentInFileCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public string Pattern { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public string Substitution { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        public string FilePath { get; set; }

        protected override void ProcessRecord()
        {
            var result = TextFileHelper.ReplaceAll(FilePath, Pattern, Substitution);
            
            WriteObject(result);
        }
    }
}
