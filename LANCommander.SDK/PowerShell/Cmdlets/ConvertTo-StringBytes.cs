using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertTo, "StringBytes")]
    [OutputType(typeof(byte[]))]
    public class ConvertToStringBytesCmdlet : BaseCmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Input { get; set; }

        [Parameter]
        public bool Utf16 { get; set; } = false;

        [Parameter]
        public bool BigEndian { get; set; } = false;

        [Parameter]
        public int MaxLength { get; set; } = 0;

        [Parameter]
        public int MinLength { get; set; } = 0;

        protected override void ProcessRecord()
        {
            byte[] output;

            if (MaxLength > 0 && Input.Length > MaxLength)
                Input = Input.Substring(0, MaxLength);

            if (MinLength > 0 && MinLength < MaxLength)
                Input = Input.PadRight(MinLength, '\0');
            else if (MinLength > 0)
                Input = Input.PadRight(MaxLength, '\0');

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
