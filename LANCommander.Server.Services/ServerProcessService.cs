﻿using AutoMapper;
using CoreRCON;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using LANCommander.SDK;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services
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
        public Data.Models.Server Server { get; private set; }
        public ServerProcessStatus Status { get; private set; }
        public Exception Exception { get; private set; }

        public ServerStatusUpdateEventArgs(Data.Models.Server server, ServerProcessStatus status)
        {
            Server = server;
            Status = status;
        }

        public ServerStatusUpdateEventArgs(Data.Models.Server server, ServerProcessStatus status, Exception exception) : this(server, status)
        {
            Exception = exception;
        }
    }

    public class LogFileMonitor : IDisposable
    {
        private ManualResetEvent Latch;
        private FileStream FileStream;
        private FileSystemWatcher FileSystemWatcher;

        public LogFileMonitor(Data.Models.Server server, ServerConsole serverConsole)
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
                                // hubContext.Clients.All.SendAsync("Log", serverConsole.ServerId, line);
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

        private readonly IMapper Mapper;
        private readonly IServiceProvider ServiceProvider;
        private readonly SDK.Client Client;

        public ServerProcessService(
            ILogger<ServerProcessService> logger,
            IMapper mapper,
            IServiceProvider serviceProvider,
            SDK.Client client) : base(logger)
        {
            Mapper = mapper;
            ServiceProvider = serviceProvider;
            Client = client;
        }

        public async Task StartServerAsync(Guid serverId)
        {
            Data.Models.Server server;

            using (var scope = ServiceProvider.CreateScope())
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                server = await serverService.GetAsync(serverId);

                // Don't start the server if it's already started
                if (GetStatus(server) != ServerProcessStatus.Stopped)
                    return;

                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Starting));

                Logger?.LogInformation("Starting server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

                foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.BeforeStart))
                {
                    try
                    {
                        var script = new PowerShellScript(SDK.Enums.ScriptType.BeforeStart);

                        // script.AddVariable("Server", Mapper.Map<SDK.Models.Server>(server));

                        script.UseWorkingDirectory(server.WorkingDirectory);
                        script.UseInline(serverScript.Contents);
                        script.UseShellExecute();

                        Logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                    }
                }

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
                        Logger?.LogInformation("Game Server {ServerName} ({ServerId}) Info: {Message}", server.Name, server.Id, e.Data);
                    });

                    process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                    {
                        Logger?.LogError("Game Server {ServerName} ({ServerId}) Error: {Message}", server.Name, server.Id, e.Data);
                    });
                }

                try
                {
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
                    OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, ex));

                    Logger?.LogError(ex, "Could not start server {ServerName} ({ServerId})", server.Name, server.Id);
                }
            }
        }


        public async void StopServerAsync(Guid serverId)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                var server = await serverService.GetAsync(serverId);

                Logger?.LogInformation("Stopping server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Stopping));

                if (Processes.ContainsKey(server.Id))
                {
                    var process = Processes[server.Id];

                    if (server.ProcessTerminationMethod == ProcessTerminationMethod.Close)
                        process.CloseMainWindow();
                    else if (server.ProcessTerminationMethod == ProcessTerminationMethod.Kill)
                        process.Kill();
                    else
                    {
                        int signal = 1;
                        int pid = process.Id;

                        process.Close();

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

                    Processes.Remove(server.Id);
                }

                if (LogFileMonitors.ContainsKey(server.Id))
                {
                    LogFileMonitors[server.Id].Dispose();
                    LogFileMonitors.Remove(server.Id);
                }

                foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.AfterStop))
                {
                    try
                    {
                        var script = new PowerShellScript(SDK.Enums.ScriptType.AfterStop);

                        // script.AddVariable("Server", Mapper.Map<SDK.Models.Server>(server));

                        script.UseWorkingDirectory(server.WorkingDirectory);
                        script.UseInline(serverScript.Contents);
                        script.UseShellExecute();

                        Logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                    }
                }

                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Stopped));
            }
        }

        private void StartMonitoringLog(ServerConsole log, Data.Models.Server server)
        {
            if (!LogFileMonitors.ContainsKey(server.Id))
            {
                LogFileMonitors[server.Id] = new LogFileMonitor(server, log);
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

        public ServerProcessStatus GetStatus(Data.Models.Server server)
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
