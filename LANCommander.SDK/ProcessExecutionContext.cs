using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using YamlDotNet.Serialization;

namespace LANCommander.SDK
{
    public class ProcessExecutionContext : IDisposable
    {
        private readonly Client Client;
        private readonly ILogger Logger;

        private Process Process;

        private Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public event DataReceivedEventHandler? OutputDataReceived;
        public event DataReceivedEventHandler? ErrorDataReceived;
        
        public ProcessExecutionContext(Client client)
        {
            Client = client;
        }

        public ProcessExecutionContext(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public void AddVariable(string key, string value)
        {
            Variables[key] = value;
        }

        public string ExpandVariables(string input, string workingDirectory, Dictionary<string, string> additionalVariables = null, bool skipSlashes = false)
        {
            try
            {
                if (input == null)
                    return input;

                foreach (var variable in Variables)
                {
                    input = input.Replace($"{{{variable.Key}}}", variable.Value);
                }

                if (additionalVariables != null)
                    foreach (var variable in additionalVariables)
                    {
                        input = input.Replace($"{{{variable.Key}}}", variable.Value);
                    }

                return input.ExpandEnvironmentVariables(workingDirectory, skipSlashes);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not expand runtime variables");

                return input;
            }
        }

        public Process GetProcess()
        {
            return Process;
        }

        public async Task ExecuteServerAsync(Models.Server server,
            CancellationToken cancellationToken = default)
        {
            Process = new Process();
            
            Process.StartInfo.Arguments = ExpandVariables(server.Arguments, server.WorkingDirectory, skipSlashes: true);
            Process.StartInfo.FileName = ExpandVariables(server.Path, server.WorkingDirectory);
            Process.StartInfo.WorkingDirectory = server.WorkingDirectory;
            Process.StartInfo.UseShellExecute = server.UseShellExecute;
            Process.EnableRaisingEvents = true;
            
            if (!Process.StartInfo.UseShellExecute)
            {
                Process.BeginErrorReadLine();
                Process.BeginOutputReadLine();
            }
            
            if (OutputDataReceived != null)
                Process.OutputDataReceived += OutputDataReceived;
            
            if (ErrorDataReceived != null)
                Process.ErrorDataReceived += ErrorDataReceived;
            
            Logger?.LogTrace("Running server executable");
            Logger?.LogTrace("Arguments: {Arguments}", Process.StartInfo.Arguments);
            Logger?.LogTrace("File Name: {FileName}", Process.StartInfo.FileName);
            Logger?.LogTrace("Working Directory: {WorkingDirectory}", Process.StartInfo.WorkingDirectory);
            
            bool exited = false;

            Process.Start();
            
            cancellationToken.WaitHandle.WaitOne();
            
            if (server.ProcessTerminationMethod == ProcessTerminationMethod.Close)
                Process.CloseMainWindow();
            else if (server.ProcessTerminationMethod == ProcessTerminationMethod.Kill)
                Process.Kill();
            else
            {
                int signal = 1;
                int pid = Process.Id;

                Process.Close();

                switch (server.ProcessTerminationMethod)
                {
                    case ProcessTerminationMethod.SIGHUP:
                        signal = 1;
                        break;
                    case ProcessTerminationMethod.SIGINT:
                        signal = 2;
                        break;
                    case ProcessTerminationMethod.SIGKILL:
                        signal = 9;
                        break;
                    case ProcessTerminationMethod.SIGTERM:
                        signal = 15;
                        break;
                }

                using (var terminator = new Process())
                {
                    terminator.StartInfo.FileName = "kill";
                    terminator.StartInfo.Arguments = $"-{signal} {pid}";
                    terminator.Start();
                    await terminator.WaitForExitAsync();
                }
            }
        }

        public async Task ExecuteGameActionAsync(string installDirectory, Guid gameId, Models.Action action, string args = "", CancellationToken cancellationToken = default)
        {
            var manifest = await ManifestHelper.ReadAsync(installDirectory, gameId);

            if (action == null)
                action = manifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);

            Process = new Process();

            Process.StartInfo.Arguments = ExpandVariables(action.Arguments, installDirectory, skipSlashes: true);
            Process.StartInfo.FileName = ExpandVariables(action.Path, installDirectory);
            Process.StartInfo.WorkingDirectory = ExpandVariables(action.WorkingDirectory, installDirectory);
            Process.StartInfo.UseShellExecute = true;
            
            if (OutputDataReceived != null)
                Process.OutputDataReceived += OutputDataReceived;
            
            if (ErrorDataReceived != null)
                Process.ErrorDataReceived += ErrorDataReceived;

            if (String.IsNullOrWhiteSpace(action.WorkingDirectory))
                Process.StartInfo.WorkingDirectory = installDirectory;

            if (!String.IsNullOrWhiteSpace(args))
                Process.StartInfo.Arguments += " " + args;

            Logger?.LogTrace("Running game executable");
            Logger?.LogTrace("Arguments: {Arguments}", Process.StartInfo.Arguments);
            Logger?.LogTrace("File Name: {FileName}", Process.StartInfo.FileName);
            Logger?.LogTrace("Working Directory: {WorkingDirectory}", Process.StartInfo.WorkingDirectory);
            Logger?.LogTrace("Manifest Path: {ManifestPath}", ManifestHelper.GetPath(installDirectory, gameId));

            bool exited = false;

            Process.Start();

            await Process.WaitForAllExitAsync(cancellationToken);
        }

        public void Dispose()
        {
            try
            {
                if (Process != null)
                {
                    if (!Process.HasExited)
                        Process.Close();

                    Process.Dispose();
                }
            }
            catch { }

            Client.Lobbies.ReleaseSteam();
        }
    }
}
