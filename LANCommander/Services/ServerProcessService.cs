using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Fluent;
using System.Diagnostics;

namespace LANCommander.Services
{
    public enum ServerProcessStatus
    {
        Retrieving,
        Stopped,
        Starting,
        Stopping,
        Running,
        Error
    }

    public class ServerLogEventArgs : EventArgs
    {
        public string Line { get; private set; }
        public ServerConsole Log { get; private set; }

        public ServerLogEventArgs(string line, ServerConsole console)
        {
            Line = line;
            Log = console;
        }
    }

    public class LogFileMonitor : IDisposable
    {
        private ManualResetEvent Latch;
        private FileStream FileStream;
        private FileSystemWatcher FileSystemWatcher;

        public LogFileMonitor(Server server, ServerConsole serverConsole, IHubContext<GameServerHub> hubContext)
        {
            var logPath = Path.Combine(server.WorkingDirectory, serverConsole.Path);

            if (File.Exists(serverConsole.Path))
            {
                var lockMe = new object();

                Latch = new ManualResetEvent(true);
                FileStream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                FileSystemWatcher = new FileSystemWatcher(Path.GetDirectoryName(logPath));

                FileSystemWatcher.Changed += (s, e) =>
                {
                    lock (lockMe)
                    {
                        if (e.FullPath != logPath)
                            return;

                        Latch.Set();
                    }
                };

                using (var sr = new StreamReader(FileStream))
                {
                    while (true)
                    {
                        Thread.Sleep(100);

                        Latch.WaitOne();

                        lock (lockMe)
                        {
                            String line;

                            while ((line = sr.ReadLine()) != null)
                            {
                                hubContext.Clients.All.SendAsync("Log", serverConsole.ServerId, line);
                                //OnLog?.Invoke(this, new ServerLogEventArgs(line, log));
                            }

                            Latch.Set();
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            if (Latch != null)
                Latch.Dispose();

            if (FileStream != null)
                FileStream.Dispose();

            if (FileSystemWatcher != null)
                FileSystemWatcher.Dispose();
        }
    }

    public class ServerProcessService : BaseService
    {
        public Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        public Dictionary<Guid, int> Threads { get; set; } = new Dictionary<Guid, int>();
        public Dictionary<Guid, LogFileMonitor> LogFileMonitors { get; set; } = new Dictionary<Guid, LogFileMonitor>();

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

            foreach (var logFile in server.ServerConsoles.Where(sc => sc.Type == ServerConsoleType.LogFile))
            {
                StartMonitoringLog(logFile, server);
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

            if (LogFileMonitors.ContainsKey(server.Id))
            {
                LogFileMonitors[server.Id].Dispose();
                LogFileMonitors.Remove(server.Id);
            }
        }

        private void StartMonitoringLog(ServerConsole log, Server server)
        {
            var logPath = Path.Combine(server.WorkingDirectory, log.Path);

            if (!LogFileMonitors.ContainsKey(server.Id))
            {
                LogFileMonitors[server.Id] = new LogFileMonitor(server, log, HubContext);
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
