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

        private static bool SupportsCurrentRuntime(System.Collections.Generic.IEnumerable<SDK.Models.Manifest.Script> scripts, Enums.ScriptType type)
        {
            var platforms = scripts?.FirstOrDefault(s => s.Type == type)?.Platforms ?? Enums.RuntimePlatform.None;

            return EnvironmentHelper.SupportsCurrentRuntime(platforms);
        }

        private static bool SupportsCurrentRuntime(System.Collections.Generic.IEnumerable<SDK.Models.Script> scripts, Enums.ScriptType type)
        {
            var platforms = scripts?.FirstOrDefault(s => s.Type == type)?.Platforms ?? Enums.RuntimePlatform.None;

            return EnvironmentHelper.SupportsCurrentRuntime(platforms);
        }

        /// <summary>
        /// Adds the IPX relay variables to a launcher-side script. Mirrors what
        /// <see cref="GameClient.RunAsync"/> puts into the process execution context so that
        /// scripts and play actions are pointed at the same relay.
        /// </summary>
        private async Task AddIPXRelayVariablesAsync(PowerShellScript script)
        {
            try
            {
                if (!connectionClient.IsConnected() || !settingsProvider.CurrentValue.IPXRelay.Enabled)
                    return;

                var host = await IPXRelayHelper.ResolveHostAsync(
                    settingsProvider.CurrentValue.IPXRelay.Host,
                    connectionClient.GetServerAddress(),
                    logger);

                if (String.IsNullOrWhiteSpace(host))
                    return;

                script.AddVariable("IPXRelayHost", host);
                script.AddVariable("IPXRelayPort", settingsProvider.CurrentValue.IPXRelay.Port.ToString());
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not populate IPX relay variables for script");
            }
        }

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
