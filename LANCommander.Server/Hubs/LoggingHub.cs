using LANCommander.Server.Settings;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Hubs
{
    public class LoggingHub(IOptions<Settings.Settings> settings) : Hub
    {
        private const int MaxHistoryLines = 500;

        public override async Task OnConnectedAsync()
        {
            var fileProvider = settings.Value.Server.Logs.Providers
                .FirstOrDefault(p => p.Type == LoggingProviderType.File && p.Enabled);

            if (fileProvider != null)
            {
                var logDirectory = fileProvider.ConnectionString;
                var logFilePath = Path.Combine(logDirectory, $"log-{DateTime.Now:yyyy-MM-dd}.txt");

                if (File.Exists(logFilePath))
                {
                    try
                    {
                        var lines = await ReadTailLinesAsync(logFilePath, MaxHistoryLines);

                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            var (message, level, timestamp) = ParseLogLine(line);

                            await Clients.Caller.SendAsync("Log", message, level, timestamp);
                        }
                    }
                    catch
                    {
                        // Don't fail the connection if history can't be read
                    }
                }
            }

            await Clients.Caller.SendAsync("Log", "Connected to server logging provider!", LogLevel.Information, DateTime.Now);
            await base.OnConnectedAsync();
        }

        public static async Task Log(IHubContext<LoggingHub> context, string message, LogLevel logLevel, DateTime timestamp)
        {
            await context.Clients.All.SendAsync("Log", message, logLevel, timestamp);
        }

        private static async Task<string[]> ReadTailLinesAsync(string filePath, int lineCount)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            var allText = await reader.ReadToEndAsync();
            var lines = allText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            return lines.Length <= lineCount
                ? lines
                : lines[^lineCount..];
        }

        private static (string message, LogLevel level, DateTime timestamp) ParseLogLine(string line)
        {
            // Format: "2025-01-18 12:34:56.789 [Information] [Category] Message"
            var level = LogLevel.Information;
            var timestamp = DateTime.Now;

            try
            {
                if (line.Length >= 23)
                {
                    if (DateTime.TryParse(line[..23], out var parsed))
                        timestamp = parsed;
                }

                var firstBracket = line.IndexOf('[');
                var closeBracket = line.IndexOf(']', firstBracket + 1);

                if (firstBracket >= 0 && closeBracket > firstBracket)
                {
                    var levelStr = line[(firstBracket + 1)..closeBracket];

                    if (Enum.TryParse<LogLevel>(levelStr, true, out var parsed))
                        level = parsed;
                }

                // The message is everything after the second closing bracket
                var secondBracket = line.IndexOf(']', closeBracket + 1);
                var message = secondBracket >= 0 && secondBracket + 1 < line.Length
                    ? line[(secondBracket + 1)..].TrimStart()
                    : line;

                return (message, level, timestamp);
            }
            catch
            {
                return (line, LogLevel.Information, DateTime.Now);
            }
        }
    }
}
