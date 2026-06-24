using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertTo, "StringBytes")]
    [OutputType(typeof(byte[]))]
    public class ConvertToStringBytesCmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true, HelpMessage = "The string to convert to a byte array.")]
        public string Input { get; set; }

        [Parameter(HelpMessage = "Encode as UTF-16 instead of ASCII. Use with -BigEndian for big-endian UTF-16.")]
        public bool Utf16 { get; set; } = false;

        [Parameter(HelpMessage = "Use big-endian byte order for UTF-16 encoding. Only applies when -Utf16 is set.")]
        public bool BigEndian { get; set; } = false;

        [Parameter(HelpMessage = "Maximum number of characters to include. The string will be truncated if longer. Set to 0 for no limit.")]
        public int MaxLength { get; set; } = 0;

        [Parameter(HelpMessage = "Minimum number of characters in the output. The string will be right-padded with null characters if shorter. Set to 0 for no padding.")]
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
