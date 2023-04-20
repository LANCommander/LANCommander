using LANCommander.Data.Models;
using NLog;
using System.Diagnostics;

namespace LANCommander.Services
{
    public enum ServerProcessStatus
    {
        Stopped,
        Starting,
        Running,
        Error
    }

    public class ServerProcessService : BaseService
    {
        public Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        public Dictionary<Guid, int> Threads { get; set; } = new Dictionary<Guid, int>();

        public async Task StartServer(Server server)
        {
            var process = new Process();

            process.StartInfo.FileName = server.Path;
            process.StartInfo.WorkingDirectory = server.WorkingDirectory;
            process.StartInfo.Arguments = server.Arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.EnableRaisingEvents = true;

            process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                Logger.Info("Game Server {ServerName} ({ServerId}) Info: {Message}", server.Name, server.Id, e.Data);
            });

            process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
            {
                Logger.Error("Game Server {ServerName} ({ServerId}) Error: {Message}", server.Name, server.Id, e.Data);
            });

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            Processes[server.Id] = process;

            await process.WaitForExitAsync();
        }


        public void StopServer(Server server)
        {
            if (Processes.ContainsKey(server.Id))
            {
                var process = Processes[server.Id];

                process.Kill();
            }
        }

        public ServerProcessStatus GetStatus(Server server)
        {
            Process process = null;

            if (Processes.ContainsKey(server.Id))
                process = Processes[server.Id];

            if (process == null || process.HasExited)
                return ServerProcessStatus.Stopped;

            if (!process.HasExited)
                return ServerProcessStatus.Running;

            if (process.ExitCode != 0)
                return ServerProcessStatus.Error;

            return ServerProcessStatus.Stopped;
        }
    }
}
