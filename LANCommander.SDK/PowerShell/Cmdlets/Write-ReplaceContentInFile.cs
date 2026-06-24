using System.Management.Automation;
using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.PowerShell.Cmdlets
{

    [Cmdlet(VerbsCommunications.Write, "ReplaceContentInFile")]
    [OutputType(typeof(string))]
    public class ReplaceContentInFileCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The regular expression pattern to search for in the file content.")]
        public string Pattern { get; set; }

        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The replacement string. Supports regex substitution groups ($1, $2, etc.).")]
        public string Substitution { get; set; }

        [Parameter(Mandatory = true, Position = 2, HelpMessage = "The path to the text file to modify.")]
        [Alias("f")]
        public string FilePath { get; set; }

        protected override void ProcessRecord()
        {
            var result = TextFileHelper.ReplaceAll(FilePath, Pattern, Substitution);
            
            WriteObject(result);
        }
    }
}
