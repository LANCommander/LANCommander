using System.IO;
using System.Management.Automation;
using System.Text.RegularExpressions;

namespace LANCommander.PowerShell.Cmdlets
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
            var contents = File.ReadAllText(FilePath);
            var regex = new Regex(Pattern, RegexOptions.Multiline);

            var result = regex.Replace(contents, Substitution);

            WriteObject(result);
        }
    }
}
