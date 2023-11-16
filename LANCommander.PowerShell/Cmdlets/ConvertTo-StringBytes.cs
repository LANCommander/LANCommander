using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertTo, "StringBytes")]
    [OutputType(typeof(byte[]))]
    public class ConvertToStringBytesCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Input { get; set; }

        [Parameter]
        public bool Utf16 { get; set; } = false;

        [Parameter]
        public bool BigEndian { get; set; } = false;

        [Parameter]
        public int MaxLength { get; set; } = 0;

        protected override void ProcessRecord()
        {
            byte[] output;

            if (MaxLength > 0 && Input.Length > MaxLength)
                Input = Input.Substring(0, MaxLength);

            if (Utf16 && BigEndian)
                output = System.Text.Encoding.BigEndianUnicode.GetBytes(Input);
            else if (Utf16)
                output = System.Text.Encoding.Unicode.GetBytes(Input);
            else
                output = System.Text.Encoding.ASCII.GetBytes(Input);

            WriteObject(output);
        }
    }
}
