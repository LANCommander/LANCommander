using LANCommander.SDK.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.PowerShell.Debugging.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Settings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellScript
    {
        public ScriptType Type { get; private set; }
        private string Contents { get; set; } = "";
        public string WorkingDirectory { get; private set; } = "";
        private bool ShellExecute { get; set; } = false;
        public bool RunAsAdmin { get; private set; } = false;
        private bool IgnoreWow64 { get; set; } = false;
        private bool Debug { get; set; } = false;
        public PowerShellVariableList Variables { get; private set; }
        public Dictionary<string, string> Arguments { get; private set; }

        /// <summary>The file passed to <see cref="UseFile"/>, or null for inline scripts.</summary>
        public string FilePath { get; private set; }

        private IntPtr _wow64 = IntPtr.Zero;
        private readonly IServiceProvider ServiceProvider;
        private readonly ILogger<PowerShellScript> Logger;
        private IEnumerable<IScriptDebugger> Debuggers { get; set; }
        private System.Management.Automation.PowerShell Context { get; set; }
        private IScriptDebugContext DebugContext { get; set; }
        private DebugSession _debugSession;

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
            Variables = [];
            Arguments = [];

            // Instantiate a new scope
            ServiceProvider = serviceProvider;
            Logger = ServiceProvider.GetRequiredService<ILogger<PowerShellScript>>();

            var settingsProvider = ServiceProvider.GetService<ISettingsProvider>();

            Debug = settingsProvider.CurrentValue.Debug.EnableScriptDebugging;

            IgnoreWow64Redirection();
        }

        public PowerShellScript UseFile(string path)
        {
            FilePath = path;

            // A debugger watching this script gets the chance to write unsaved edits or a draft to the
            // file first, so what runs (and what an elevated child process reads) is what the editor shows.
            if (Identity is { } identity)
            {
                try
                {
                    Broker?.PrepareScriptFile(identity, path);
                }
                catch (Exception ex)
                {
                    Logger?.LogWarning(ex, "The script debugger could not prepare {ScriptPath}", path);
                }
            }

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
                if (_debugSession is { } session)
                    session.RequestStop();
                else
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

        private IScriptDebugBroker Broker => ServiceProvider.GetService<IScriptDebugBroker>();

        /// <summary>
        /// What this script is, for the script debugger: derived from the <c>.lancommander/&lt;id&gt;/&lt;Type&gt;.ps1</c>
        /// path it was loaded from and the manifest variables it was given. Null for inline scripts.
        /// </summary>
        public ScriptIdentity Identity
        {
            get
            {
                if (!ScriptHelper.TryParseScriptFilePath(FilePath, out var installDirectory, out var ownerId, out var type))
                    return null;

                var ownerKind = HasVariable("RedistributableManifest") ? ScriptOwnerKind.Redistributable
                    : HasVariable("ToolManifest") ? ScriptOwnerKind.Tool
                    : ScriptOwnerKind.Game;

                var gameId = (Variables.FirstOrDefault(v => v.Name == "GameManifest")?.Value as LANCommander.SDK.Models.Manifest.Game)?.Id;

                return new ScriptIdentity(new ScriptKey(ownerId, type), ownerKind, gameId, installDirectory, FilePath);
            }
        }

        /// <summary>True when a script debugger will attach to this script when it runs.</summary>
        public bool IsDebuggerAttached
        {
            get
            {
                try
                {
                    return Identity is { } identity && Broker?.IsAttached(identity) == true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        private bool HasVariable(string name) => Variables.Any(v => v.Name == name);

        private bool RequiresAdmin()
        {
            var pattern = @"^[ \t]*#(\s?Requires\s?Admin|Requires -RunAsAdministrator)";

            return Regex.IsMatch(Contents, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        }

        public async Task<T?> ExecuteAsync<T>()
        {
            var attachment = await TryAttachDebuggerAsync();

            if (attachment is not null)
                return await ExecuteUnderDebuggerAsync<T>(attachment);

            T? result = default;

            DisableWow64Redirection();

            var builder = new PowerShellRunspaceBuilder(ServiceProvider, Logger);

            using Runspace runspace = builder.Open();

            builder.Initialize(runspace, WorkingDirectory, Type, Variables, Logo);

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

            if (Debug && HasLegacyDebuggers())
            {
                AddDebugHeader();
            }

            try
            {
                Logger.LogInformation("Executing PowerShell script of type {ScriptType}", Type);
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

                if (returnValue is null && results is { Count: > 0 })
                    returnValue = results[^1];

                if (returnValue is not null)
                {
                    result = ConvertResult<T>(returnValue);

                    if (result is null)
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

                if (Debug && HasLegacyDebuggers())
                {
                    await DebugAsync(async dbg =>
                    {
                        await dbg.BreakAsync(DebugContext);
                    });
                }

                Context.Dispose();
            }

            RevertWow64Redirection();

            return result;
        }

        private async Task<ScriptDebugAttachment> TryAttachDebuggerAsync()
        {
            var broker = Broker;

            if (broker is null || Identity is not { } identity)
                return null;

            try
            {
                return await broker.TryAttachAsync(identity);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "The script debugger could not attach to the {ScriptType} script; running it normally", Type);
                return null;
            }
        }

        /// <summary>
        /// Runs the script under the interactive debugger. The runspace is prepared exactly as it is for a
        /// normal run, and the result is read back the same way, so the script cannot tell the difference.
        /// </summary>
        private async Task<T?> ExecuteUnderDebuggerAsync<T>(ScriptDebugAttachment attachment)
        {
            var builder = new PowerShellRunspaceBuilder(ServiceProvider, Logger);
            var session = new DebugSession(attachment.Sink);

            _debugSession = session;

            try
            {
                try
                {
                    attachment.SessionStarted(session);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "The script debugger failed to observe the {ScriptType} script session", Type);
                }

                var variableNames = String.Join(", ", Variables.Select(v => "$" + v.Name));

                attachment.Sink.WriteLine(ConsoleOutputKind.System, $"[debugger] {Type} script attached: {FilePath}");
                attachment.Sink.WriteLine(ConsoleOutputKind.System, $"[debugger] Variables: {variableNames}");

                Logger.LogInformation("Executing PowerShell script of type {ScriptType} under the script debugger", Type);

                session.Start(new DebugLaunchRequest
                {
                    ScriptPath = FilePath,
                    ScriptContents = Contents,
                    Breakpoints = attachment.Breakpoints,
                    StepIntoOnStart = attachment.StepIntoOnStart,
                    Preamble = "Write-Host $Logo",
                    OnPipelineThreadStarted = DisableWow64Redirection,
                    OnPipelineThreadCompleted = RevertWow64Redirection,
                    OpenRunspace = host =>
                    {
                        var runspace = builder.Open(host, PSThreadOptions.UseCurrentThread);

                        builder.Initialize(runspace, WorkingDirectory, Type, Variables, Logo);

                        return runspace;
                    },
                    CaptureResult = (powerShell, output) =>
                    {
                        foreach (var error in powerShell.Streams.Error)
                            Logger.LogError("Script error: {InvocationName} : {ErrorMessage}", error.InvocationInfo?.InvocationName, error.Exception?.Message);

                        var returnValue = powerShell.Runspace.SessionStateProxy.PSVariable.GetValue("Return");

                        if (returnValue is null && output.Count > 0)
                            returnValue = output[^1];

                        if (returnValue is null)
                        {
                            Logger.LogWarning("Script did not return a value via $Return or the pipeline");
                            return null;
                        }

                        var converted = ConvertResult<T>(returnValue);

                        if (converted is null)
                            Logger.LogWarning("Script returned a value but it could not be converted to {ExpectedType}", typeof(T).Name);

                        return converted;
                    },
                });

                var completion = await session.Completion;

                if (completion.Faulted)
                    Logger.LogError("Could not execute script: {ErrorMessage}", completion.ErrorMessage);

                return completion.Result is T typed ? typed : default;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not execute script");

                return default;
            }
            finally
            {
                _debugSession = null;
                session.Dispose();
            }
        }

        private bool HasLegacyDebuggers()
        {
            Debuggers ??= ServiceProvider.GetServices<IScriptDebugger>();

            return Debuggers.Any();
        }

        private void AddDebugHeader()
        {
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

        private static T? ConvertResult<T>(object value)
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
            Debuggers ??= ServiceProvider.GetServices<IScriptDebugger>();

            foreach (var debugger in Debuggers)
            {
                await action.Invoke(debugger);
            }
        }

        private async void Error_DataAdded(object? sender, DataAddedEventArgs e)
        {
            if (sender is null)
                return;

            var record = ((PSDataCollection<ErrorRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Error, "{InvocationName} : {ExceptionMessage}", record.InvocationInfo.InvocationName, record.Exception.Message);
                await dbg.OutputAsync(DebugContext, LogLevel.Error, record.InvocationInfo.PositionMessage);
            });
        }

        private async void Warning_DataAdded(object? sender, DataAddedEventArgs e)
        {
            if (sender is null)
                return;

            var record = ((PSDataCollection<WarningRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Warning, record.Message);
            });
        }

        private async void Debug_DataAdded(object? sender, DataAddedEventArgs e)
        {
            if (sender is null)
                return;

            var record = ((PSDataCollection<DebugRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Debug, record.Message);
            });
        }

        private async void Verbose_DataAdded(object? sender, DataAddedEventArgs e)
        {
            if (sender is null)
                return;

            var record = ((PSDataCollection<VerboseRecord>)sender)[e.Index];

            await DebugAsync(async dbg =>
            {
                await dbg.OutputAsync(DebugContext, LogLevel.Trace, record.Message);
            });
        }

        private async void Information_DataAdded(object? sender, DataAddedEventArgs e)
        {
            if (sender is null)
                return;

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
