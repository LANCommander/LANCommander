using System;
using System.IO;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.Edit, "PatchBinary")]
    public class EditPatchBinaryCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, HelpMessage = "The byte offset within the file where the data should be written.")]
        public long Offset { get; set; }

        [Parameter(Mandatory = true, Position = 1, HelpMessage = "The byte array to write at the specified offset.")]
        public byte[] Data { get; set; }

        [Parameter(Mandatory = true, Position = 2, HelpMessage = "The path to the binary file to patch.")]
        [Alias("f")]
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
