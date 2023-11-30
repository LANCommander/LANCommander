using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertTo, "SerializedBase64")]
    [OutputType(typeof(object))]
    public class ConvertToSerializedBase64Cmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public object Input { get; set; }

        protected override void ProcessRecord()
        {
            var output = Convert.ToBase64String(Encoding.UTF8.GetBytes(PSSerializer.Serialize(Input)));

            WriteObject(output);
        }
    }
}
