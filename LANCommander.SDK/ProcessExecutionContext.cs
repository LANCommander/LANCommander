using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;

namespace LANCommander.SDK
{
    public class ProcessExecutionContext(
        ILogger<ProcessExecutionContext> logger,
        LobbyClient lobbyClient) : IDisposable
    {
        private Process Process;

        private Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public event DataReceivedEventHandler? OutputDataReceived;
        public event DataReceivedEventHandler? ErrorDataReceived;

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
                logger?.LogError(ex, "Could not expand runtime variables");

                return input;
            }
        }

        public Process GetProcess()
        {
            return Process;
        }

        public async Task ExecuteServerAsync(Models.Server server,
            CancellationTokenSource cancellationTokenSource = default)
        {
            Process = new Process();
            Process.EnableRaisingEvents = true;

            var processStartInfo = new ProcessStartInfo();
            
            processStartInfo.Arguments = ExpandVariables(server.Arguments, server.WorkingDirectory, skipSlashes: true);
            processStartInfo.FileName = ExpandVariables(server.Path, server.WorkingDirectory);
            processStartInfo.WorkingDirectory = server.WorkingDirectory;
            processStartInfo.UseShellExecute = server.UseShellExecute;
            
            if (!server.UseShellExecute)
            {
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardOutput = true;
            }
            
            Process.StartInfo = processStartInfo;
            
            if (OutputDataReceived != null && !processStartInfo.UseShellExecute)
                Process.OutputDataReceived += OutputDataReceived;
            
            if (OutputDataReceived != null && !processStartInfo.UseShellExecute)
                Process.ErrorDataReceived += ErrorDataReceived;
            
            logger?.LogTrace("Running server executable");
            logger?.LogTrace("Arguments: {Arguments}", Process.StartInfo.Arguments);
            logger?.LogTrace("File Name: {FileName}", Process.StartInfo.FileName);
            logger?.LogTrace("Working Directory: {WorkingDirectory}", Process.StartInfo.WorkingDirectory);
            
            bool exited = false;

            Process.Start();

            Process.Exited += (sender, args) =>
            {
                if (cancellationTokenSource?.Token.CanBeCanceled ?? false)
                    cancellationTokenSource.Cancel();
            };
            
            if (processStartInfo.RedirectStandardError)
                Process.BeginErrorReadLine();
            
            if (processStartInfo.RedirectStandardOutput)
                Process.BeginOutputReadLine();
            
            cancellationTokenSource?.Token.WaitHandle.WaitOne();
            
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

        public async Task ExecuteGameActionAsync(string installDirectory, Guid gameId, Models.Manifest.Action action, string args = "", CancellationToken cancellationToken = default)
        {
            var manifest = await ManifestHelper.ReadAsync<Models.Manifest.Game>(installDirectory, gameId);

            if (action == null)
                action = manifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);
            
            if (manifest.CustomFields != null && manifest.CustomFields.Any())
            {
                foreach (var customField in manifest.CustomFields)
                {
                    AddVariable(customField.Name, customField.Value);
                }
            }

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

            logger?.LogTrace("Running game executable");
            logger?.LogTrace("Arguments: {Arguments}", Process.StartInfo.Arguments);
            logger?.LogTrace("File Name: {FileName}", Process.StartInfo.FileName);
            logger?.LogTrace("Working Directory: {WorkingDirectory}", Process.StartInfo.WorkingDirectory);
            logger?.LogTrace("Manifest Path: {ManifestPath}", ManifestHelper.GetPath(installDirectory, gameId));

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

            lobbyClient.ReleaseSteam();
        }
    }
}
