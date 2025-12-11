using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.Launcher.Services;
using System.Runtime.InteropServices;
using LANCommander.Launcher.Data;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

var logsDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
if (!Directory.Exists(logsDirectory))
    Directory.CreateDirectory(logsDirectory);

builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders();
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
    loggingBuilder.AddFile(logsDirectory);
    loggingBuilder.SetMinimumLevel(LogLevel.Information);
    loggingBuilder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
    loggingBuilder.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Warning);
    loggingBuilder.AddFilter("AntDesign", LogLevel.Warning);
});

builder.Services.AddLANCommanderClient<LANCommander.SDK.Models.Settings>();
builder.Services.AddLANCommanderLauncher(options =>
{

});

using IHost host = builder.Build();

host.Services.InitializeLANCommander();

using (var scope = host.Services.CreateScope())
{
    var connectionClient = scope.ServiceProvider.GetRequiredService<IConnectionClient>();
    var settingsProvider = scope.ServiceProvider.GetRequiredService<SettingsProvider<LANCommander.Launcher.Settings.Settings>>();
    var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
    var commandLineService = scope.ServiceProvider.GetRequiredService<CommandLineService>();

    if (!await connectionClient.PingAsync())
        await connectionClient.EnableOfflineModeAsync();

    if (settingsProvider.CurrentValue.Games.InstallDirectories.Length == 0)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:", "Games")];
            });
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
            });
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            settingsProvider.Update(s =>
            {
                s.Games.InstallDirectories = [Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Games")];
            });
    }

    await databaseContext.Database.MigrateAsync();
    
    await commandLineService.ParseCommandLineAsync(args);
}

/// <summary>
/// Extension method to add file logging provider to ILoggingBuilder
/// </summary>
static class LoggingExtensions
{
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string logDirectory)
    {
        return builder.AddProvider(new FileLoggerProvider(logDirectory));
    }

    private class FileLogger : ILogger
    {
        private readonly string _logDirectory;
        private readonly string _categoryName;

        public FileLogger(string logDirectory, string categoryName)
        {
            _logDirectory = logDirectory;
            _categoryName = categoryName;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var logFilePath = Path.Combine(
                _logDirectory,
                $"log-{DateTime.Now:yyyy-MM-dd}.txt");

            var message = formatter(state, exception);
            var logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] [{_categoryName}] {message}";

            if (exception != null)
                logMessage += Environment.NewLine + exception;

            try
            {
                lock (_logDirectory)
                {
                    File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if logging to file fails
            }
        }
    }

    private class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logDirectory;

        public FileLoggerProvider(string logDirectory)
        {
            _logDirectory = logDirectory;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_logDirectory, categoryName);
        }

        public void Dispose() { }
    }
}