using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertTo, "SerializedBase64")]
    [OutputType(typeof(object))]
    public class ConvertToSerializedBase64Cmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public object Input { get; set; }

        protected override void ProcessRecord()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            var output = Convert.ToBase64String(Encoding.UTF8.GetBytes(serializer.Serialize(Input)));

            WriteObject(output);
        }
    }
}
