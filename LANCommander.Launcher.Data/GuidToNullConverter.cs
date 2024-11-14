using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Data
{
    internal class GuidToNullConverter : ValueConverter<Guid?, Guid?>
    {
        public GuidToNullConverter(ConverterMappingHints? mappingHints = null) : base(x => x == Guid.Empty ? default : x, x => x, mappingHints)
        { }
    }
}
