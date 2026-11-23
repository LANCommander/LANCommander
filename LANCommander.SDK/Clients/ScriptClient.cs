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

        /// <summary>
        /// Adds <c>$BaseInstallDirectory</c> and <c>$BaseGameManifest</c> so addon scripts can
        /// reach the game they extend, wherever the addon itself was installed. For a main game
        /// they're the same as <c>$InstallDirectory</c> and <c>$GameManifest</c>; for an addon whose
        /// base game can't be found they're null.
        /// </summary>
        private void AddBaseGameVariables(PowerShellScript script, string installDirectory, SDK.Models.Manifest.Game manifest)
        {
            string baseInstallDirectory = null;
            SDK.Models.Manifest.Game baseManifest = null;

            try
            {
                baseInstallDirectory = GameClient.GetBaseInstallDirectory(installDirectory, manifest);

                if (manifest == null || !manifest.IsAddon || manifest.BaseGameId == Guid.Empty)
                    baseManifest = manifest;
                else if (baseInstallDirectory != null)
                    baseManifest = ManifestHelper.Read<SDK.Models.Manifest.Game>(baseInstallDirectory, manifest.BaseGameId);

                if (baseInstallDirectory == null)
                    logger?.LogWarning("Could not find the install directory of base game {BaseGameId} for addon {GameTitle}", manifest?.BaseGameId, manifest?.Title);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not resolve the base game for script variables");
            }

            script.AddVariable("BaseInstallDirectory", baseInstallDirectory);
            script.AddVariable("BaseGameManifest", baseManifest);
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
