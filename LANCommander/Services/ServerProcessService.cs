using LANCommander.Data.Models;
using System.Diagnostics;

namespace LANCommander.Services
{
    public class ServerProcessService
    {
        public Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        public Dictionary<Guid, int> Threads { get; set; } = new Dictionary<Guid, int>();

        public async Task StartServer(Server server)
        {
            var process = new Process();

            process.StartInfo.FileName = server.Path;
            process.StartInfo.WorkingDirectory = server.WorkingDirectory;
            process.StartInfo.Arguments = server.Arguments;
            process.StartInfo.UseShellExecute = true;

            process.Start();

            Processes[server.Id] = process;

            await process.WaitForExitAsync();
        }

        public void StopServer(Server server)
        {
            if (Processes.ContainsKey(server.Id))
                Processes[server.Id].Kill();
        }
    }
}
