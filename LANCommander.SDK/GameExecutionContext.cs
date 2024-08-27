using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LANCommander.SDK
{
    public class GameExecutionContext : IDisposable
    {
        private readonly Client Client;
        private readonly ILogger Logger;

        public Process Process { get; private set; } = new Process();

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

        public async Task ExecuteAsync(string installDirectory, Guid gameId, Guid actionId)
        {
            var manifest = await ManifestHelper.ReadAsync(installDirectory, gameId);
            var action = manifest.Actions.FirstOrDefault(a => a.Id == actionId);

            if (action == null)
                action = manifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);

            Process.StartInfo.Arguments = ExpandVariables(action.Arguments, installDirectory, skipSlashes: true);
            Process.StartInfo.FileName = ExpandVariables(action.Path, installDirectory);
            Process.StartInfo.WorkingDirectory = ExpandVariables(action.WorkingDirectory, installDirectory);
            Process.StartInfo.UseShellExecute = true;

            if (String.IsNullOrWhiteSpace(action.WorkingDirectory))
                Process.StartInfo.WorkingDirectory = installDirectory;

            Process.Start();

            await Process.WaitForExitAsync();
        }

        public void Dispose()
        {
            if (Process != null)
            {
                if (!Process.HasExited)
                    Process.Close();

                Process.Dispose();
            }
        }
    }
}
