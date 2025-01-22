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
using YamlDotNet.Serialization;

namespace LANCommander.SDK
{
    public class GameExecutionContext : IDisposable
    {
        private readonly Client Client;
        private readonly ILogger Logger;

        private Process Process;

        private Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public GameExecutionContext(Client client)
        {
            Client = client;
        }

        public GameExecutionContext(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public void AddVariable(string key, string value)
        {
            Variables[key] = value;
        }

        public string ExpandVariables(string input, string installDirectory, Dictionary<string, string> additionalVariables = null, bool skipSlashes = false)
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

                return input.ExpandEnvironmentVariables(installDirectory, skipSlashes);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not expand runtime variables");

                return input;
            }
        }

        public async Task ExecuteAsync(string installDirectory, Guid gameId, Models.Action action, string args = "", CancellationToken cancellationToken = default)
        {
            var manifest = await ManifestHelper.ReadAsync(installDirectory, gameId);

            if (action == null)
                action = manifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);

            Process = new Process();

            Process.StartInfo.Arguments = ExpandVariables(action.Arguments, installDirectory, skipSlashes: true);
            Process.StartInfo.FileName = ExpandVariables(action.Path, installDirectory);
            Process.StartInfo.WorkingDirectory = ExpandVariables(action.WorkingDirectory, installDirectory);
            Process.StartInfo.UseShellExecute = true;

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
