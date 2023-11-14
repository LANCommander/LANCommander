using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "GameManifest")]
    [OutputType(typeof(GameManifest))]
    public class GetGameManifestCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Path { get; set; }

        protected override void ProcessRecord()
        {
            WriteObject(ManifestHelper.Read(Path));
        }
    }
}
