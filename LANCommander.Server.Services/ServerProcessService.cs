using AutoMapper;
using CoreRCON;
using LANCommander.Server.Data.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.PowerShell;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net;
using LANCommander.SDK;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
        public Dictionary<Guid, CancellationTokenSource> Running { get; set; } = new();
        public Dictionary<Guid, LogFileMonitor> LogFileMonitors { get; set; } = new();

        private Dictionary<Guid, RCON> RconConnections { get; set; } = new();
        private Dictionary<Guid, ServerProcessStatus> Status { get; set; } = new();

        public delegate void OnLogHandler(object sender, ServerLogEventArgs e);
        public event OnLogHandler OnLog;
        public event EventHandler<ServerStatusUpdateEventArgs> OnStatusUpdate;
        
        private readonly IServiceProvider ServiceProvider;
        private readonly SDK.Client Client;
        private readonly IMapper Mapper;

        public ServerProcessService(
            ILogger<ServerProcessService> logger,
            IServiceProvider serviceProvider,
            SDK.Client client,
            IMapper mapper) : base(logger)
        {
            ServiceProvider = serviceProvider;
            Client = client;
            Mapper = mapper;
        }

        public async Task StartServerAsync(Guid serverId)
        {
            Data.Models.Server server;

            using (var scope = ServiceProvider.CreateScope())
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                server = await serverService
                    .Query(q =>
                    {
                        return q
                            .Include(s => s.Scripts)
                            .Include(s => s.Game)
                            .Include(s => s.ServerConsoles);
                    }).GetAsync(serverId);

                // Don't start the server if it's already started
                if (GetStatus(server) != ServerProcessStatus.Stopped)
                    return;
                
                UpdateStatus(server, ServerProcessStatus.Starting);

                _logger?.LogInformation("Starting server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

                foreach (var serverScript in server.Scripts.Where(s => s.Type == ScriptType.BeforeStart))
                {
                    try
                    {
                        var script = new PowerShellScript(SDK.Enums.ScriptType.BeforeStart);

                        script.AddVariable("Server", Mapper.Map<SDK.Models.Server>(server));

                        script.UseWorkingDirectory(server.WorkingDirectory);
                        script.UseInline(serverScript.Contents);
                        script.UseShellExecute();

                        _logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                    }
                }

                using (var executionContext = new ProcessExecutionContext(Client, _logger))
                {
                    try
                    {
                        executionContext.AddVariable("ServerId", server.Id.ToString());
                        executionContext.AddVariable("ServerName", server.Name);
                        executionContext.AddVariable("ServerHost", server.Host);
                        executionContext.AddVariable("ServerPort", server.Port.ToString());

                        if (server.Game != null)
                        {
                            executionContext.AddVariable("GameTitle", server.Game?.Title);
                            executionContext.AddVariable("GameId", server.Game?.Id.ToString());   
                        }
                    
                        foreach (var logFile in server.ServerConsoles.Where(sc => sc.Type == ServerConsoleType.LogFile))
                        {
                            StartMonitoringLog(logFile, server);
                        }
                    
                        UpdateStatus(server, ServerProcessStatus.Running);
                        
                        var cancellationTokenSource = new CancellationTokenSource();
                        
                        Running[server.Id] = cancellationTokenSource;

                        await executionContext.ExecuteServerAsync(Mapper.Map<SDK.Models.Server>(server), cancellationTokenSource);
                        
                        if (Running.ContainsKey(server.Id))
                            Running.Remove(server.Id);
                        
                        UpdateStatus(server, ServerProcessStatus.Stopped);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus(server, ServerProcessStatus.Error, ex);

                        _logger?.LogError(ex, "Could not start server {ServerName} ({ServerId})", server.Name, server.Id);
                    }

                }
            }
        }


        public async void StopServerAsync(Guid serverId)
        {
            using (var scope = ServiceProvider.CreateScope())
            {
                var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

                var server = await serverService
                    .Query(q =>
                    {
                        return q
                            .Include(s => s.Scripts)
                            .Include(s => s.Game)
                            .Include(s => s.ServerConsoles);
                    }).GetAsync(serverId);

                _logger?.LogInformation("Stopping server \"{ServerName}\" for game {GameName}", server.Name, server.Game?.Title);

                UpdateStatus(server, ServerProcessStatus.Stopping);

                if (Running.ContainsKey(server.Id))
                {
                    await Running[server.Id].CancelAsync();

                    Running.Remove(server.Id);
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

                        script.AddVariable("Server", Mapper.Map<SDK.Models.Server>(server));

                        script.UseWorkingDirectory(server.WorkingDirectory);
                        script.UseInline(serverScript.Contents);
                        script.UseShellExecute();

                        _logger?.LogInformation("Executing script \"{ScriptName}\"", serverScript.Name);

                        if (Client.Scripts.Debug)
                            script.EnableDebug();

                        await script.ExecuteAsync<int>();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error running script \"{ScriptName}\" for server \"{ServerName}\"", serverScript.Name, server.Name);
                    }
                }

                UpdateStatus(server, ServerProcessStatus.Stopped);
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

        private void UpdateStatus(Data.Models.Server server, ServerProcessStatus status, Exception ex = null)
        {
            if (ex != null)
            {
                Status[server.Id] = ServerProcessStatus.Error;
                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, ServerProcessStatus.Error, ex));
            }
            else if (!Status.ContainsKey(server.Id))
            {
                Status[server.Id] = status;
                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, status));
            }
            else if (Status[server.Id] != status)
            {
                Status[server.Id] = status;
                OnStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, status));
            }
        }
        
        public ServerProcessStatus GetStatus(Data.Models.Server server)
        {
            if (server == null)
                return ServerProcessStatus.Stopped;

            return GetStatus(server.Id);
        }

        public ServerProcessStatus GetStatus(Guid serverId)
        {
            if (Running.ContainsKey(serverId) && Running[serverId].IsCancellationRequested)
                return ServerProcessStatus.Stopping;
            
            if (Running.ContainsKey(serverId) && !Running[serverId].IsCancellationRequested)
                return ServerProcessStatus.Running;

            return ServerProcessStatus.Stopped;
        }
    }
}
