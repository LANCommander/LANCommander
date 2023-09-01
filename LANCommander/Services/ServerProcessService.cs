using CoreRCON;
using LANCommander.Data.Enums;
using LANCommander.Data.Models;
using LANCommander.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NLog;
using NLog.Fluent;
using System.Diagnostics;
using System.Net;

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

    public class ServerStatusUpdateEventArgs : EventArgs
    {
        public Server Server { get; private set; }
        public ServerProcessStatus Status { get; private set; }

        public ServerStatusUpdateEventArgs(Server server, ServerProcessStatus status)
        {
            Server = server;
            Status = status;
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

    public class RconConnection
    {
        public RCON RCON { get; set; }
        public LogReceiver LogReceiver { get; set; }

        public RconConnection(string host, int port, string password)
        {
            RCON = new RCON(new IPEndPoint(IPAddress.Parse(host), port), password);
        }
    }

    public class ServerProcessService : BaseService
    {
        public Dictionary<Guid, Process> Processes = new Dictionary<Guid, Process>();
        public Dictionary<Guid, int> Threads { get; set; } = new Dictionary<Guid, int>();
        public Dictionary<Guid, LogFileMonitor> LogFileMonitors { get; set; } = new Dictionary<Guid, LogFileMonitor>();

        private Dictionary<Guid, RCON> RconConnections { get; set; } = new Dictionary<Guid, RCON>();

        public delegate void OnLogHandler(object sender, ServerLogEventArgs e);
        public event OnLogHandler OnLog;

        public delegate void OnStatusUpdateHandler(object sender, ServerStatusUpdateEventArgs e);
        public event OnStatusUpdateHandler OnStatusUpdate;

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
                    HubContext.Clients.All.SendAsync("Log", e.Data);
                    Logger.Info("Game Server {ServerName} ({ServerId}) Info: {Message}", server.Name, server.Id, e.Data);
                });

                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    HubContext.Clients.All.SendAsync("Log", e.Data);
                    Logger.Error("Game Server {ServerName} ({ServerId}) Error: {Message}", server.Name, server.Id, e.Data);
                });
            }

            try
            {
                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Starting));

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

                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Running));

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error));

                Logger.Error(ex, "Could not start server process");
            }
        }


        public void StopServer(Server server)
        {
            OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Stopping));

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

            OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Stopped));
        }

        private void StartMonitoringLog(ServerConsole log, Server server)
        {
            if (!LogFileMonitors.ContainsKey(server.Id))
            {
                LogFileMonitors[server.Id] = new LogFileMonitor(server, log, HubContext);
            }
        }

        public RCON RconConnect(ServerConsole console)
        {
            if (!RconConnections.ContainsKey(console.Id))
            {
                var rcon = new RCON(new IPEndPoint(IPAddress.Parse(console.Host), console.Port.GetValueOrDefault()), console.Password);

                RconConnections[console.Id] = rcon;

                return rcon;
            }
            else
                return RconConnections[console.Id];
        }

        public async Task<string> RconSendCommandAsync(string command, ServerConsole console)
        {
            if (RconConnections.ContainsKey(console.Id))
            {
                return await RconConnections[console.Id].SendCommandAsync(command);
            }
            else
                return "";
        }

        public ServerProcessStatus GetStatus(Server server)
        {
            Process process = null;

            if (server == null)
                return ServerProcessStatus.Stopped;

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
