using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsCommon.Get, "GameManifest")]
    [OutputType(typeof(SDK.Models.Manifest.Game))]
    public class GetGameManifestCmdlet : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Path { get; set; }

        [Parameter(Mandatory = true)]
        public Guid Id { get; set; }

        protected override void ProcessRecord()
        {
            WriteObject(ManifestHelper.Read<SDK.Models.Manifest.Game>(Path, Id));
        }
    }
}
