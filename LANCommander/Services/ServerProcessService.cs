using LANCommander.Data.Models;
using LANCommander.Hubs;
using Microsoft.AspNetCore.SignalR;
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

    public class ServerLogEventArgs : EventArgs
    {
        public string Line { get; private set; }
        public ServerLog Log { get; private set; }

        public ServerLogEventArgs(string line, ServerLog log)
        {
            Line = line;
            Log = log;
        }
    }

    public class ServerProcessService : BaseService
    {
        public Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        public Dictionary<Guid, int> Threads { get; set; } = new Dictionary<Guid, int>();

        public delegate void OnLogHandler(object sender, ServerLogEventArgs e);
        public event OnLogHandler OnLog;

        private IHubContext<GameServerHub> HubContext;

        public ServerProcessService(IHubContext<GameServerHub> hubContext)
        {
            HubContext = hubContext;
        }

        public async Task StartServerAsync(Server server)
        {
            var process = new Process();

            process.StartInfo.FileName = server.Path;
            process.StartInfo.WorkingDirectory = server.WorkingDirectory;
            process.StartInfo.Arguments = server.Arguments;
            process.StartInfo.UseShellExecute = server.UseShellExecute;
            process.EnableRaisingEvents = true;

            if (!process.StartInfo.UseShellExecute)
            {
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    Logger.Info("Game Server {ServerName} ({ServerId}) Info: {Message}", server.Name, server.Id, e.Data);
                });

                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    Logger.Error("Game Server {ServerName} ({ServerId}) Error: {Message}", server.Name, server.Id, e.Data);
                });
            }

            process.Start();

            if (!process.StartInfo.UseShellExecute)
            {
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
            }

            Processes[server.Id] = process;

            foreach (var log in server.ServerLogs)
            {
                MonitorLog(log, server);
            }

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

        private void MonitorLog(ServerLog log, Server server)
        {
            var logPath = Path.Combine(server.WorkingDirectory, log.Path);

            if (File.Exists(logPath))
            {
                var lockMe = new object();
                using (var latch = new ManualResetEvent(true))
                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var fsw = new FileSystemWatcher(Path.GetDirectoryName(logPath)))
                {
                    fsw.Changed += (s, e) =>
                    {
                        lock (lockMe)
                        {
                            if (e.FullPath != logPath)
                                return;

                            latch.Set();
                        }
                    };

                    using (var sr = new StreamReader(fs))
                    {
                        while (true)
                        {
                            Thread.Sleep(100);

                            latch.WaitOne();

                            lock(lockMe)
                            {
                                String line;

                                while ((line = sr.ReadLine()) != null)
                                {
                                    HubContext.Clients.All.SendAsync("Log", log.ServerId, line);
                                    //OnLog?.Invoke(this, new ServerLogEventArgs(line, log));
                                }

                                latch.Set();
                            }
                        }
                    }
                }
            }
        }

        private void Fsw_Changed(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
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
