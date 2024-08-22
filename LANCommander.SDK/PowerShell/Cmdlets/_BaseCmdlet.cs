using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Cmdlets
{
    public abstract class BaseCmdlet : Cmdlet
    {
        public static Client Client { get; set; }
    }
}
