using LANCommander.SDK.Extensions;
using System.IO;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "SanitizedPath")]
    [OutputType(typeof(string))]
    public class GetSanitizedPathCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The path to sanitize.")]
        public string Path { get; set; }

        protected override void ProcessRecord()
        {
            WriteObject(Path.SanitizeFilename());
        }
    }
}
