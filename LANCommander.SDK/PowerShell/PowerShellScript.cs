using LANCommander.SDK.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace LANCommander.SDK.PowerShell
{
    public class PowerShellScript
    {
        private string Contents { get; set; }           = "";
        private string WorkingDirectory { get; set; }   = "";
        private bool AsAdmin { get; set; }              = false;
        private bool ShellExecute { get; set; }         = false;
        private bool IgnoreWow64 { get; set; }          = false;
        private ICollection<PowerShellVariable> Variables { get; set; }
        private Dictionary<string, string> Arguments { get; set; }
        private Process Process { get; set; }

        public PowerShellScript()
        {
            Variables = new List<PowerShellVariable>();
            Arguments = new Dictionary<string, string>();
            Process = new Process();

            Process.StartInfo.FileName = "powershell.exe";
            Process.StartInfo.RedirectStandardOutput = false;

            AddArgument("ExecutionPolicy", "Unrestricted");
        }

        public PowerShellScript UseFile(string path)
        {
            Contents = File.ReadAllText(path);

            return this;
        }

        public PowerShellScript UseInline(string contents)
        {
            Contents = contents;

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

        public PowerShellScript RunAsAdmin()
        {
            AsAdmin = true;

            Process.StartInfo.Verb = "runas";
            Process.StartInfo.UseShellExecute = true;

            return this;
        }

        public PowerShellScript IgnoreWow64Redirection()
        {
            IgnoreWow64 = true;

            return this;
        }

        public int Execute()
        {
            var scriptBuilder = new StringBuilder();

            var wow64Value = IntPtr.Zero;

            foreach (var variable in Variables)
            {
                scriptBuilder.AppendLine($"${variable.Name} = Convert-FromSerializedBase64 \"{Serialize(variable.Value)}\"");
            }

            scriptBuilder.AppendLine(Contents);

            var path = ScriptHelper.SaveTempScript(scriptBuilder.ToString());

            AddArgument("File", path);

            if (IgnoreWow64)
                Wow64DisableWow64FsRedirection(ref wow64Value);

            Process.StartInfo.Arguments = String.Join(" ", Arguments.Select((name, value) =>
            {
                return $"-{name} {value}";
            }));

            if (!String.IsNullOrEmpty(WorkingDirectory))
                Process.StartInfo.WorkingDirectory = WorkingDirectory;

            if (ShellExecute)
                Process.StartInfo.UseShellExecute = true;

            if (AsAdmin)
            {
                Process.StartInfo.Verb = "runas";
                Process.StartInfo.UseShellExecute = true;
            }

            Process.Start();
            Process.WaitForExit();

            if (IgnoreWow64)
                Wow64RevertWow64FsRedirection(ref wow64Value);

            if (File.Exists(path))
                File.Delete(path);

            return Process.ExitCode;
        }

        public static string Serialize<T>(T input)
        {
            // Use the PowerShell serializer to generate XML for our input. Then convert to base64 so we can put it on one line.
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(System.Management.Automation.PSSerializer.Serialize(input)));
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool Wow64RevertWow64FsRedirection(ref IntPtr ptr);
    }
}
