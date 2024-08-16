using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellLog
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
    }
}
