using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Cmdlets;
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

        public Func<System.Management.Automation.PowerShell, Task> OnDebug;

        public PowerShellScript(ScriptType type)
        {
            Type = type;
            Variables = new PowerShellVariableList();
            Arguments = new Dictionary<string, string>();

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

        public PowerShellScript EnableDebug()
        {
            Debug = true;

            return this;
        }

        public PowerShellScript AsAdmin()
        {
            RunAsAdmin = true;

            return this;
        }

        public async Task<int> ExecuteAsync()
        {
            var scriptBuilder = new StringBuilder();

            var wow64Value = IntPtr.Zero;

            foreach (var variable in Variables)
            {
                scriptBuilder.AppendLine($"${variable.Name} = ConvertFrom-SerializedBase64 \"{Serialize(variable.Value)}\"");
            }

            scriptBuilder.AppendLine(Contents);

            if (Debug)
            {
                scriptBuilder.AppendLine("Write-Host '----- DEBUG -----'");
                scriptBuilder.AppendLine("Write-Host 'Variables:'");
                
                foreach (var variable in Variables)
                {
                    scriptBuilder.AppendLine($"Write-Host '    ${variable.Name}'");
                }

                scriptBuilder.AppendLine("Write-Host ''");

                // Process.StartInfo.Arguments += " -NoExit";
            }

            if (IgnoreWow64)
                Wow64DisableWow64FsRedirection(ref wow64Value);

            using (Runspace runspace = RunspaceFactory.CreateRunspace(InitialSessionState))
            {
                runspace.Open();

                runspace.SessionStateProxy.Path.SetLocation(WorkingDirectory);

                using (var ps = System.Management.Automation.PowerShell.Create())
                {
                    ps.Runspace = runspace;

                    ps.AddScript(scriptBuilder.ToString());

                    var results = await ps.InvokeAsync();

                    if (Debug)
                        await OnDebug?.Invoke(ps);
                }
            }

            if (IgnoreWow64)
                Wow64RevertWow64FsRedirection(ref wow64Value);

            return 0;
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
