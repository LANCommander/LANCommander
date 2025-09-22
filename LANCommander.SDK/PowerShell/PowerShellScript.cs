using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Cmdlets;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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

        private InitialSessionState InitialSessionState { get; set; }

        private TaskCompletionSource<string> Input { get; set; }

        public PowerShellDebugHandler DebugHandler { get; private set; }

        private const string Logo = @"
   __   ___   _  _______                              __       
  / /  / _ | / |/ / ___/__  __ _  __ _  ___ ____  ___/ /__ ____
 / /__/ __ |/    / /__/ _ \/  ' \/  ' \/ _ `/ _ \/ _  / -_) __/
/____/_/ |_/_/|_/\___/\___/_/_/_/_/_/_/\_,_/_//_/\_,_/\__/_/   

";

        public PowerShellScript(ScriptType type)
        {
            Type = type;
            Variables = new PowerShellVariableList();
            Arguments = new Dictionary<string, string>();
            DebugHandler = new PowerShellDebugHandler();

            InitialSessionState = InitialSessionState.CreateDefault();
            
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Convert-AspectRatio", typeof(ConvertAspectRatioCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertFrom-SerializedBase64", typeof(ConvertFromSerializedBase64Cmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertTo-SerializedBase64", typeof(ConvertToSerializedBase64Cmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("ConvertTo-StringBytes", typeof(ConvertToStringBytesCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Edit-PatchBinary", typeof(EditPatchBinaryCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-GameManifest", typeof(GetGameManifestCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-PrimaryDisplay", typeof(GetPrimaryDisplayCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Update-IniValue", typeof(UpdateIniValueCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Write-GameManifest", typeof(WriteGameManifestCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Write-ReplaceContentInFile", typeof(ReplaceContentInFileCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-UserCustomField", typeof(GetUserCustomFieldCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Update-UserCustomField", typeof(UpdateUserCustomFieldCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-HorizontalFov", typeof(GetHorizontalFovCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Get-VerticalFov", typeof(GetVerticalFovCmdlet), null));
            InitialSessionState.Commands.Add(new SessionStateCmdletEntry("Out-PlayerAvatar", typeof(OutPlayerAvatarCmdlet), null));

            IgnoreWow64Redirection();
        }

        public PowerShellScript UseFile(string path)
        {
            Contents = File.ReadAllText(path);

            if (Contents.StartsWith("# Requires Admin"))
                RunAsAdmin = true;

            return this;
        }

        public PowerShellScript UseInline(string contents)
        {
            Contents = contents;

            if (Contents.StartsWith("# Requires Admin"))
                RunAsAdmin = true;

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

        public PowerShellScript EnableDebug(PowerShellDebugHandler debugHandler = null)
        {
            Debug = true;
            
            if (debugHandler != null)
                DebugHandler = debugHandler;

            return this;
        }

        public PowerShellScript AsAdmin()
        {
            RunAsAdmin = true;

            return this;
        }

        public async Task<T> ExecuteAsync<T>()
        {
            T result = default;
            var wow64Value = IntPtr.Zero;

            if (IgnoreWow64)
                Wow64DisableWow64FsRedirection(ref wow64Value);

            using (Runspace runspace = RunspaceFactory.CreateRunspace(InitialSessionState))
            {
                runspace.Open();

                runspace.SessionStateProxy.Path.SetLocation(WorkingDirectory);

                foreach (var variable in Variables)
                {
                    runspace.SessionStateProxy.SetVariable(variable.Name, variable.Value);
                }

                runspace.SessionStateProxy.SetVariable("Logo", Logo);
                runspace.SessionStateProxy.SetVariable("ScriptType", Type);
                runspace.SessionStateProxy.SetVariable("WorkingDirectory", WorkingDirectory);

                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = runspace;

                    if (Debug)
                        await (DebugHandler.OnDebugStart?.Invoke(ps) ?? Task.CompletedTask);

                    ps.AddScript("Write-Host $Logo");

                    ps.AddScript(Contents);

                    if (Debug)
                    {
                        ps.AddScript("Write-Host '--------- DEBUG ---------'");
                        ps.AddScript("Write-Host \"Script Type: $ScriptType\"");
                        ps.AddScript("Write-Host \"Working Directory: $WorkingDirectory\"");
                        ps.AddScript("Write-Host 'Variables:'");

                        foreach (var variable in Variables)
                        {
                            ps.AddScript($"Write-Host '    ${variable.Name}'");
                        }

                        ps.AddScript("Write-Host ''");
                        ps.AddScript("Write-Host 'Enter \"exit\" to continue'");

                        if (DebugHandler.OnOutput != null)
                        {
                            ps.Streams.Information.DataAdded += Information_DataAdded;
                            ps.Streams.Verbose.DataAdded += Verbose_DataAdded;
                            ps.Streams.Debug.DataAdded += Debug_DataAdded;
                            ps.Streams.Warning.DataAdded += Warning_DataAdded;
                            ps.Streams.Error.DataAdded += Error_DataAdded;
                        }
                    }

                    var results = await ps.InvokeAsync();

                    if (Debug)
                        await (DebugHandler.OnDebugBreak?.Invoke(ps) ?? Task.CompletedTask);

                    try
                    {
                        var returnValue = ps.Runspace.SessionStateProxy.PSVariable.GetValue("Return");

                        if (returnValue != null)
                            result = (T)returnValue;
                    }
                    catch
                    {
                        // Couldn't case properly, fallback to default
                    }
                }
            }

            if (IgnoreWow64)
                Wow64RevertWow64FsRedirection(ref wow64Value);

            return result;
        }

        private void Error_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<ErrorRecord>)sender)[e.Index];

            DebugHandler.OnOutput?.Invoke(LogLevel.Error, $"{record.InvocationInfo.InvocationName} : {record.Exception.Message}");
            DebugHandler.OnOutput?.Invoke(LogLevel.Error, record.InvocationInfo.PositionMessage);
        }

        private void Warning_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<WarningRecord>)sender)[e.Index];

            DebugHandler.OnOutput?.Invoke(LogLevel.Warning, record.Message);
        }

        private void Debug_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<DebugRecord>)sender)[e.Index];

            DebugHandler.OnOutput?.Invoke(LogLevel.Debug, record.Message);
        }

        private void Verbose_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<VerboseRecord>)sender)[e.Index];

            DebugHandler.OnOutput?.Invoke(LogLevel.Trace, record.Message);
        }

        private void Information_DataAdded(object sender, DataAddedEventArgs e)
        {
            var record = ((PSDataCollection<InformationRecord>)sender)[e.Index];

            if (record.MessageData != null && record.MessageData is HostInformationMessage)
                DebugHandler.OnOutput?.Invoke(LogLevel.Information, (record.MessageData as HostInformationMessage).Message);
        }

        public static string Serialize<T>(T input)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            // Use the YamlDotNet serializer to generate a string for our input. Then convert to base64 so we can put it on one line.
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(serializer.Serialize(input)));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64RevertWow64FsRedirection(ref IntPtr ptr);
    }
}
