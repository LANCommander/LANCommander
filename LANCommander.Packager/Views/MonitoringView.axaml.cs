using System.Collections.Concurrent;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using LANCommander.Packager.Models;
using LANCommander.Packager.Services;

namespace LANCommander.Packager.Views;

public partial class MonitoringView : UserControl
{
    private readonly PackageContext _context;
    private readonly StringBuilder _logText = new();
    private readonly ConcurrentQueue<string> _pendingLogEntries = new();
    private InstallerMonitorService? _monitorService;
    private DispatcherTimer? _updateTimer;
    private volatile int _lastFileCount;
    private volatile int _lastRegistryCount;
    private volatile bool _installerExited;

    public event Action? MonitoringCompleted;

    public MonitoringView(PackageContext context)
    {
        _context = context;
        InitializeComponent();
    }

    public void StartMonitoring()
    {
        _monitorService = new InstallerMonitorService();

        _monitorService.OnFileChange += entry =>
        {
            _lastFileCount = _monitorService.FileChangeCount;
        };

        _monitorService.OnRegistryChange += entry =>
        {
            _lastRegistryCount = _monitorService.RegistryChangeCount;
        };

        _monitorService.OnInstallerExited += () =>
        {
            _installerExited = true;
            _pendingLogEntries.Enqueue("Installer exited.");
        };

        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _updateTimer.Tick += (_, _) => FlushUpdates();
        _updateTimer.Start();

        StatusLabel.Text = "Preparing...";

        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "packager.log");
        
        File.WriteAllText(logPath, $"=== Packager started {DateTime.Now} ==={Environment.NewLine}");

        Task.Run(() =>
        {
            try
            {
                Action<string> log = msg =>
                {
                    try
                    {
                        File.AppendAllText(logPath, msg + Environment.NewLine);
                    } 
                    catch { }
                    
                    _pendingLogEntries.Enqueue(msg);
                };

                var mode = _monitorService.LaunchInstaller(_context.InstallerPath, log);

                Dispatcher.UIThread.Post(() =>
                {
                    StatusLabel.Text = mode == "snapshot"
                        ? "Installer running (snapshot mode). Waiting..."
                        : "Installer running (Interposer). Monitoring...";
                });
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    StatusLabel.Text = "Error - see packager.log";

                    _pendingLogEntries.Enqueue($"Exception: [{ex.GetType().Name}] {ex.Message}");
                    _pendingLogEntries.Enqueue($"Installer: {_context.InstallerPath}");

                    if (ex.InnerException != null)
                        _pendingLogEntries.Enqueue($"Inner: {ex.InnerException.Message}");

                    _pendingLogEntries.Enqueue(ex.StackTrace ?? "");

                    try
                    {
                        File.AppendAllLines(logPath, new[]
                        {
                            $"Exception: [{ex.GetType().Name}] {ex.Message}",
                            ex.StackTrace ?? ""
                        });
                    }
                    catch { }
                });
            }
        });
    }

    private void FlushUpdates()
    {
        FileCountLabel.Text = $"Files changed: {_lastFileCount}";
        RegistryCountLabel.Text = $"Registry entries: {_lastRegistryCount}";

        bool hasNew = false;
        
        while (_pendingLogEntries.TryDequeue(out var entry))
        {
            _logText.AppendLine(entry);
            hasNew = true;
        }

        if (hasNew)
        {
            EventLog.Text = _logText.ToString();
            EventLog.CaretIndex = EventLog.Text.Length;
        }

        if (_installerExited)
        {
            _installerExited = false;
            _updateTimer?.Stop();

            // Dispose the monitor service to stop all background Interposer activity
            _context.FileChanges = _monitorService?.FileChanges.ToList() ?? [];
            _context.RegistryChanges = _monitorService?.RegistryChanges.ToList() ?? [];
            _monitorService?.Dispose();
            _monitorService = null;

            // Final drain
            while (_pendingLogEntries.TryDequeue(out var remaining))
                _logText.AppendLine(remaining);

            EventLog.Text = _logText.ToString();
            EventLog.CaretIndex = EventLog.Text.Length;

            FileCountLabel.Text = $"Files changed: {_lastFileCount}";
            RegistryCountLabel.Text = $"Registry entries: {_lastRegistryCount}";
            StatusLabel.Text = "Done! Press Next to continue.";

            MonitoringCompleted?.Invoke();
        }
    }

    public InstallerMonitorService? GetMonitorService() => _monitorService;
}
