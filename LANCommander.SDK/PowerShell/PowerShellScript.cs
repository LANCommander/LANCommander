using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.PowerShell.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Settings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellScript
    {
        public ScriptType Type { get; private set; }
        private string Contents { get; set; }           = "";
        public string WorkingDirectory { get; private set; }   = "";
        private bool ShellExecute { get; set; }         = false;
        public bool RunAsAdmin { get; private set; }       = false;
        private bool IgnoreWow64 { get; set; }          = false;
        private bool Debug { get; set; }                = false;
        public PowerShellVariableList Variables { get; private set; }
        public Dictionary<string, string> Arguments { get; private set; }

        private TaskCompletionSource<string> Input { get; set; }
        
        private IntPtr _wow64 = IntPtr.Zero;
        private readonly IServiceProvider ServiceProvider;
        private readonly ILogger<PowerShellScript> Logger;
        private IEnumerable<IScriptDebugger> Debuggers { get; set; }
        private System.Management.Automation.PowerShell Context { get; set; }
        private IScriptDebugContext DebugContext { get; set; }

        private const string Logo = @"
   __   ___   _  _______                              __       
  / /  / _ | / |/ / ___/__  __ _  __ _  ___ ____  ___/ /__ ____
 / /__/ __ |/    / /__/ _ \/  ' \/  ' \/ _ `/ _ \/ _  / -_) __/
/____/_/ |_/_/|_/\___/\___/_/_/_/_/_/_/\_,_/_//_/\_,_/\__/_/   

";

        public PowerShellScript(
            IServiceProvider serviceProvider,
            ScriptType type,
            IOptions<Settings> settings)
        {
            
            Type = type;
            Variables = new PowerShellVariableList();
            Arguments = new Dictionary<string, string>();
            
            // Instantiate a new scope
            ServiceProvider = serviceProvider;
            Logger = ServiceProvider.GetRequiredService<ILogger<PowerShellScript>>();

            var settingsProvider = ServiceProvider.GetService<ISettingsProvider>();
            
            Debug = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;
            
            IgnoreWow64Redirection();
        }

        public PowerShellScript UseFile(string path)
        {
            Contents = File.ReadAllText(path);

            if (RequiresAdmin())
                AsAdmin();

            return this;
        }

        public PowerShellScript UseInline(string contents)
        {
            Contents = contents;

            if (RequiresAdmin())
                AsAdmin();

            return this;
        }

        public PowerShellScript UseWorkingDirectory(string path)
        {
            WorkingDirectory = path;

            return this;
        }

        public PowerShellScript UseShellExecute()
        {
            ShellExecute = true;

            return this;
        }

        public PowerShellScript AddVariable<T>(string name, T value)
        {
            Variables.Add(new PowerShellVariable(name, value, typeof(T)));

            return this;
        }

        public PowerShellScript AddArgument<T>(string name, T value)
        {
            Arguments.Add(name, $"\"{value}\"");

            return this;
        }

        public PowerShellScript AddArgument(string name, int value)
        {
            Arguments[name] = value.ToString();

            return this;
        }

        public PowerShellScript AddArgument(string name, long value)
        {
            Arguments[name] = value.ToString();

            return this;
        }

        public PowerShellScript IgnoreWow64Redirection()
        {
            IgnoreWow64 = true;

            return this;
        }

        public PowerShellScript EnableDebug()
        {
            Debug = true;

            return this;
        }

        public void Stop()
        {
            try
            {
                Context?.Stop();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning(ex, "Error stopping PowerShell pipeline");
            }
        }

        public PowerShellScript AsAdmin()
        {
            RunAsAdmin = true;

            return this;
        }

        private bool RequiresAdmin()
        {
            var pattern = @"^#(\s?Requires\s?Admin|Requires -RunAsAdministrator)\s*";
            
            return Regex.IsMatch(Contents, pattern);
        }

        public async Task<T> ExecuteAsync<T>()
        {
            T result = default;
            
            var initialSessionState = InitialSessionState.CreateDefault();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                initialSessionState.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;

            initialSessionState.AddCustomCmdlets();

            DisableWow64Redirection();

            using (Runspace runspace = RunspaceFactory.CreateRunspace(initialSessionState))
            {
                runspace.Open();
                
                var modulesPath = AppPaths.GetConfigPath("Modules");
                
                if (Directory.Exists(modulesPath))
                {
                    foreach (var moduleDirectory in Directory.GetDirectories(modulesPath))
                    {
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
                                    Logger.LogWarning("Failed to load module {ModuleDirectory}: {ErrorMessage}", moduleDirectory, error.Exception?.Message);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning(ex, "Failed to load module {ModuleDirectory}", moduleDirectory);
                        }
                    }
                }

                // Ensure TLS 1.2 is available for web requests (GitHub, etc.)
                using (var tls = System.Management.Automation.PowerShell.Create())
                {
                    tls.Runspace = runspace;
                    tls.AddScript("[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12");
                    tls.Invoke();
                }

                runspace.SessionStateProxy.Path.SetLocation(WorkingDirectory);

                foreach (var variable in Variables)
                {
                    runspace.SessionStateProxy.SetVariable(variable.Name, variable.Value);
                }

                runspace.SessionStateProxy.SetVariable("Logo", Logo);
                runspace.SessionStateProxy.SetVariable("ScriptType", Type);
                runspace.SessionStateProxy.SetVariable("WorkingDirectory", WorkingDirectory);
                
                // Store services in session state for cmdlets to access
                var settingsProvider = ServiceProvider.GetService<ISettingsProvider>();
                if (settingsProvider != null)
                {
                    runspace.SessionStateProxy.SetVariable("LANCommander.SDK.ISettingsProvider", settingsProvider);
                }

                var apiRequestFactory = ServiceProvider.GetService<ApiRequestFactory>();
                if (apiRequestFactory != null)
                {
                    runspace.SessionStateProxy.SetVariable("LANCommander.SDK.ApiRequestFactory", apiRequestFactory);
                }
                
                // Logger will be created when first cmdlet runs and sets host UI in session state (see AsyncCmdlet)
                
                Context = System.Management.Automation.PowerShell.Create();

                Context.Runspace = runspace;

                DebugContext = new PowerShellDebugContext(Context);

                await DebugAsync(async dbg =>
                {
                    await dbg.StartAsync(DebugContext);
                });

                Context.AddScript("Write-Host $Logo");
                Context.AddScript(Contents);

                Context.Streams.Information.DataAdded += Information_DataAdded;
                Context.Streams.Verbose.DataAdded += Verbose_DataAdded;
                Context.Streams.Debug.DataAdded += Debug_DataAdded;
                Context.Streams.Warning.DataAdded += Warning_DataAdded;
                Context.Streams.Error.DataAdded += Error_DataAdded;

                if (Debug) {
                    Context.AddScript("Write-Host '--------- DEBUG ---------'");
                    Context.AddScript("Write-Host \"Script Type: $ScriptType\"");
                    Context.AddScript("Write-Host \"Working Directory: $WorkingDirectory\"");
                    Context.AddScript("Write-Host 'Variables:'");

                    foreach (var variable in Variables)
                    {
                        Context.AddScript($"Write-Host '    ${variable.Name}'");
                    }

                    Context.AddScript("Write-Host ''");
                    Context.AddScript("Write-Host 'Enter \"exit\" to continue'");
                }

                try
                {
                    var results = await Context.InvokeAsync();

                    if (Context.HadErrors)
                    {
                        foreach (var error in Context.Streams.Error)
                        {
                            Logger.LogError("Script error: {InvocationName} : {ErrorMessage}", error.InvocationInfo?.InvocationName, error.Exception?.Message);

                            await DebugAsync(async dbg =>
                            {
                                await dbg.OutputAsync(DebugContext, LogLevel.Error, "{InvocationName} : {ErrorMessage}", error.InvocationInfo?.InvocationName, error.Exception?.Message);
                            });
                        }
                    }

                    var returnValue = Context.Runspace.SessionStateProxy.PSVariable.GetValue("Return");

                    if (returnValue == null && results != null && results.Count > 0)
                        returnValue = results[results.Count - 1];

                    if (returnValue != null)
                    {
                        result = ConvertResult<T>(returnValue);

                        if (result == null)
                            Logger.LogWarning("Script returned a value but it could not be converted to {ExpectedType}", typeof(T).Name);
                    }
                    else
                    {
                        Logger.LogWarning("Script did not return a value via $Return or the pipeline");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not execute script");
                }
                finally
                {
                    await DebugAsync(async dbg =>
                    {
                        await dbg.EndAsync(DebugContext);
                    });

                    if (Debug)
                    {
                        await DebugAsync(async dbg =>
                        {
                            await dbg.BreakAsync(DebugContext);
                        });
                    }

                    Context.Dispose();
                }
            }

            RevertWow64Redirection();

            return result;
        }

        private static T ConvertResult<T>(object value)
        {
            // Unwrap PSObject wrapper
            var psObj = value as PSObject;
            var raw = psObj?.BaseObject ?? value;

            // Direct cast if the underlying object is already the right type
            if (raw is T typed)
                return typed;

            // Map PSObject properties onto a new instance of T by name
            if (psObj != null)
            {
                try
                {
                    var instance = Activator.CreateInstance<T>();
                    var targetProps = typeof(T).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    foreach (var targetProp in targetProps)
                    {
                        if (!targetProp.CanWrite)
                            continue;

                        var psProp = psObj.Properties[targetProp.Name];

                        if (psProp == null)
                            continue;

                        var psValue = psProp.Value;

                        if (psValue is PSObject psValObj)
                            psValue = psValObj.BaseObject;

                        if (psValue != null && targetProp.PropertyType.IsAssignableFrom(psValue.GetType()))
                            targetProp.SetValue(instance, psValue);
                        else if (psValue != null)
                            targetProp.SetValue(instance, Convert.ChangeType(psValue, targetProp.PropertyType));
                    }

                    return instance;
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }

        private async Task DebugAsync(Func<IScriptDebugger, Task> action)
        {
            if (Debuggers == null)
                Debuggers = ServiceProvider.GetServices<IScriptDebugger>();

            foreach (var debugger in Debuggers)
            {
                await action.Invoke(debugger);
            }
        }

        private async void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<ErrorRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Error, "{InvocationName} : {ExceptionMessage}", record.InvocationInfo.InvocationName, record.Exception.Message);
                await dbg.OutputAsync(DebugContext, LogLevel.Error, record.InvocationInfo.PositionMessage);
            });
        }

        private async void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<WarningRecord>)sender)[e.Index];
            
            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Warning, record.Message);
            });
        }

        private async void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<DebugRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Debug, record.Message);
            });
        }

        private async void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<VerboseRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Trace, record.Message);
            });
        }

        private async void Information_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<InformationRecord>)sender)[e.Index];
            
            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Information, (record.MessageData as HostInformationMessage).Message);
            });
        }

        public static string Serialize<T>(T input)
        {
            var serializer = YamlSerializerFactory.Create();

            // Use the YamlDotNet serializer to generate a string for our input. Then convert to base64 so we can put it on one line.
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(serializer.Serialize(input)));
        }

        private void DisableWow64Redirection()
        {
            try
            {
                if (IgnoreWow64)
                    Wow64DisableWow64FsRedirection(ref _wow64);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not disable Wow64 Redirection");
            }
        }

        private void RevertWow64Redirection()
        {
            try
            {
                if (IgnoreWow64 && _wow64 != IntPtr.Zero)
                    Wow64RevertWow64FsRedirection(ref _wow64);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not revert Wow64 Redirection");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64RevertWow64FsRedirection(ref IntPtr ptr);
    }
}
