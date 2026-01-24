using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    [Cmdlet(VerbsData.ConvertFrom, "SerializedBase64")]
    [OutputType(typeof(object))]
    public class ConvertFromSerializedBase64Cmdlet : Cmdlet
    {
        [Parameter(Mandatory = true, Position = 0, ValueFromPipeline = true, ValueFromPipelineByPropertyName = true)]
        public string Input { get; set; }

        protected override void ProcessRecord()
        {
            var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(Input));

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            WriteObject(deserializer.Deserialize<object>(yaml));
        }
    }
}
