using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.PowerShell.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.PowerShell
{
    /// <summary>
    /// Builds and prepares the runspaces LANCommander scripts run in: the custom and plugin cmdlets,
    /// installed modules, TLS 1.2, the working directory, script variables and the services cmdlets read
    /// from session state. Shared by normal execution, the script debugger, and the debugger's command
    /// catalogue so all three see the same session.
    /// </summary>
    public sealed class PowerShellRunspaceBuilder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        public PowerShellRunspaceBuilder(IServiceProvider serviceProvider, ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// Builds the runspace configuration. When <paramref name="bypassExecutionPolicy"/> is set we
        /// prefer an execution policy of <see cref="Microsoft.PowerShell.ExecutionPolicy.Bypass"/> so
        /// unsigned game scripts run without prompting.
        /// </summary>
        public InitialSessionState CreateSessionState(bool bypassExecutionPolicy)
        {
            var initialSessionState = InitialSessionState.CreateDefault();

            if (bypassExecutionPolicy)
                initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

            initialSessionState.AddCustomCmdlets();

            RegisterPluginCmdlets(initialSessionState);

            return initialSessionState;
        }

        /// <summary>
        /// Opens a PowerShell runspace, preferring an execution policy of Bypass on Windows. Applying a
        /// process-scope Bypass during <see cref="Runspace.Open"/> can throw on machines where the
        /// execution policy is locked down by Group Policy; in that case we fall back to opening the
        /// runspace with the system default policy so script execution is never silently skipped.
        /// </summary>
        /// <param name="host">A custom host, or null for the engine's default host.</param>
        /// <param name="threadOptions">Thread options to apply before opening, or null for the default.</param>
        public Runspace Open(PSHost host = null, PSThreadOptions? threadOptions = null)
        {
            var bypassExecutionPolicy = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            var runspace = Create(host, CreateSessionState(bypassExecutionPolicy), threadOptions);

            try
            {
                runspace.Open();

                return runspace;
            }
            catch (Exception ex) when (bypassExecutionPolicy)
            {
                _logger?.LogWarning(ex, "Failed to open PowerShell runspace with ExecutionPolicy.Bypass; retrying with the system default execution policy");

                runspace.Dispose();

                var fallback = Create(host, CreateSessionState(false), threadOptions);

                fallback.Open();

                return fallback;
            }
        }

        private static Runspace Create(PSHost host, InitialSessionState sessionState, PSThreadOptions? threadOptions)
        {
            var runspace = host is null
                ? RunspaceFactory.CreateRunspace(sessionState)
                : RunspaceFactory.CreateRunspace(host, sessionState);

            if (threadOptions.HasValue)
                runspace.ThreadOptions = threadOptions.Value;

            return runspace;
        }

        /// <summary>Imports the modules installed in the config Modules directory and by plugins.</summary>
        public void ImportModules(Runspace runspace)
        {
            var modulesPath = AppPaths.GetConfigPath("Modules");

            var moduleSources = new List<string>();

            if (Directory.Exists(modulesPath))
                moduleSources.AddRange(Directory.GetDirectories(modulesPath));

            moduleSources.AddRange(GetPluginModulePaths());

            foreach (var moduleDirectory in moduleSources)
            {
                ImportModuleIntoRunspace(runspace, moduleDirectory);
            }
        }

        /// <summary>
        /// Prepares an open runspace to run a script: modules, TLS 1.2, working directory, the script's
        /// variables plus <c>$Logo</c>/<c>$ScriptType</c>/<c>$WorkingDirectory</c>, and the services the
        /// LANCommander cmdlets read from session state.
        /// </summary>
        public void Initialize(Runspace runspace, string workingDirectory, ScriptType type, IEnumerable<PowerShellVariable> variables, string logo)
        {
            ImportModules(runspace);

            // Ensure TLS 1.2 is available for web requests (GitHub, etc.)
            using (var tls = System.Management.Automation.PowerShell.Create())
            {
                _logger.LogInformation("Ensuring TLS 1.2 is enabled for PowerShell runspace");
                tls.Runspace = runspace;
                tls.AddScript("[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12");
                tls.Invoke();
            }

            runspace.SessionStateProxy.Path.SetLocation(workingDirectory);

            foreach (var variable in variables)
            {
                _logger.LogInformation("Setting PowerShell variable ${VariableName} = {VariableValue}", variable.Name, variable.Value);
                runspace.SessionStateProxy.SetVariable(variable.Name, variable.Value);
            }

            runspace.SessionStateProxy.SetVariable("Logo", logo);
            runspace.SessionStateProxy.SetVariable("ScriptType", type);
            runspace.SessionStateProxy.SetVariable("WorkingDirectory", workingDirectory);

            // Store services in session state for cmdlets to access
            var settingsProvider = _serviceProvider.GetService<ISettingsProvider>();
            if (settingsProvider is not null)
            {
                runspace.SessionStateProxy.SetVariable(ScriptServicesProvider.SettingsProviderKey, settingsProvider);
            }

            var apiRequestFactory = _serviceProvider.GetService<ApiRequestFactory>();
            if (apiRequestFactory is not null)
            {
                runspace.SessionStateProxy.SetVariable(ScriptServicesProvider.ApiRequestFactoryKey, apiRequestFactory);
            }

            var profileClient = _serviceProvider.GetService<Services.ProfileClient>();
            if (profileClient is not null)
            {
                runspace.SessionStateProxy.SetVariable(ScriptServicesProvider.ProfileClientKey, profileClient);
            }

            // Logger will be created when first cmdlet runs and sets host UI in session state (see AsyncCmdlet)
        }

        private void RegisterPluginCmdlets(InitialSessionState initialSessionState)
        {
            IEnumerable<IPluginPowerShellExtension> extensions;

            try
            {
                extensions = _serviceProvider.GetServices<IPluginPowerShellExtension>();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not resolve plugin PowerShell extensions");
                return;
            }

            foreach (var extension in extensions)
            {
                IEnumerable<Type> cmdletTypes;

                try
                {
                    cmdletTypes = extension.GetCmdletTypes() ?? Enumerable.Empty<Type>();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Plugin PowerShell extension {Extension} failed to enumerate cmdlet types", extension.GetType().FullName);
                    continue;
                }

                foreach (var cmdletType in cmdletTypes)
                {
                    try
                    {
                        var attribute = cmdletType.GetCustomAttribute<CmdletAttribute>();

                        if (attribute == null)
                        {
                            _logger?.LogWarning("Plugin cmdlet type {CmdletType} is missing a [Cmdlet] attribute and was skipped", cmdletType.FullName);
                            continue;
                        }

                        var cmdletName = $"{attribute.VerbName}-{attribute.NounName}";

                        initialSessionState.Commands.Add(new SessionStateCmdletEntry(cmdletName, cmdletType, null));

                        _logger?.LogDebug("Registered plugin cmdlet {CmdletName} from {CmdletType}", cmdletName, cmdletType.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Could not register plugin cmdlet {CmdletType}", cmdletType.FullName);
                    }
                }
            }
        }

        private IEnumerable<string> GetPluginModulePaths()
        {
            IEnumerable<IPluginPowerShellExtension> extensions;

            try
            {
                extensions = _serviceProvider.GetServices<IPluginPowerShellExtension>();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not resolve plugin PowerShell extensions");
                yield break;
            }

            foreach (var extension in extensions)
            {
                IEnumerable<string> modulePaths;

                try
                {
                    modulePaths = extension.GetModulePaths() ?? Enumerable.Empty<string>();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Plugin PowerShell extension {Extension} failed to enumerate module paths", extension.GetType().FullName);
                    continue;
                }

                foreach (var modulePath in modulePaths)
                {
                    if (!string.IsNullOrWhiteSpace(modulePath))
                        yield return modulePath;
                }
            }
        }

        private void ImportModuleIntoRunspace(Runspace runspace, string moduleDirectory)
        {
            _logger.LogInformation("Importing PowerShell module from {ModuleDirectory}", moduleDirectory);
            try
            {
                using var import = System.Management.Automation.PowerShell.Create();

                import.Runspace = runspace;

                import.AddCommand("Import-Module")
                    .AddParameter("Name", moduleDirectory)
                    .AddParameter("ErrorAction", "Stop");

                import.Invoke();

                if (import.HadErrors)
                    foreach (var error in import.Streams.Error)
                        _logger.LogWarning("Failed to load module {ModuleDirectory}: {ErrorMessage}", moduleDirectory, error.Exception?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load module {ModuleDirectory}", moduleDirectory);
            }
        }
    }
}
