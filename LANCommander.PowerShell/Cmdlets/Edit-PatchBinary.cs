using System;
using System.IO;
using System.Management.Automation;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Edit, "PatchBinary")]
    [OutputType(typeof(string))]
    public class EditPatchBinaryCmdlet : PSCmdlet
    {
        [Parameter(Mandatory = true, Position = 0)]
        public long Offset { get; set; }

        [Parameter(Mandatory = true, Position = 1)]
        public byte[] Data { get; set; }

        [Parameter(Mandatory = true, Position = 2)]
        public string FilePath { get; set; }

        protected override void ProcessRecord()
        {
            using (var writer = File.OpenWrite(FilePath))
            {
                writer.Seek(Offset, SeekOrigin.Begin);

                writer.Write(Data, 0, Data.Length);
            }
        }
    }
}
