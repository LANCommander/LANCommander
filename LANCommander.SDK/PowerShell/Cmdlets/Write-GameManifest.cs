using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommunications.Write, "GameManifest")]
    [OutputType(typeof(string))]
    public class WriteGameManifestCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The directory path where the game manifest file should be written.")]
        public string Path { get; set; }

        [Parameter(Mandatory = true, Position = 1, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The game manifest object to serialize and write to disk.")]
        public SDK.Models.Manifest.Game Manifest { get; set; }

        protected override void ProcessRecord()
        {
            var destination = ManifestHelper.Write(Manifest, Path);

            WriteObject(destination);
        }
    }
}
