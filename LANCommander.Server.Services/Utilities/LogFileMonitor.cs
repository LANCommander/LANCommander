using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Utilities;

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