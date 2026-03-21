using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services
{
    public partial class ScriptClient(
        ILogger<ScriptClient> logger,
        IServiceProvider serviceProvider,
        ISettingsProvider settingsProvider,
        PowerShellScriptFactory powerShellScriptFactory,
        IConnectionClient connectionClient)
    {
        public bool Debug { get; set; }

        private async Task<bool> RunScriptExternallyAsync(PowerShellScript script)
        {
            var scriptRunners = serviceProvider.GetServices<IScriptInterceptor>();

            foreach (var scriptRunner in scriptRunners)
            {
                if (await scriptRunner.ExecuteAsync(script))
                    return true;
            }

            return false;
        }
    }
}
