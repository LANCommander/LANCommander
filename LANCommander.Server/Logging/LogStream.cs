using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Logging;

public readonly record struct LogEntry(string Message, LogLevel Level, DateTime Timestamp);

/// <summary>
/// In-process feed of application log entries. <see cref="SignalRLoggerProvider"/> publishes to it,
/// and the server's own log viewer subscribes to it directly rather than connecting to
/// <see cref="Hubs.LoggingHub"/> (which is for authenticated administrators only).
/// </summary>
public sealed class LogStream(IOptions<Settings.Settings> settings)
{
    private const int MaxHistoryLines = 500;

    public event Action<LogEntry>? Logged;

    public void Publish(LogEntry entry)
    {
        try
        {
            Logged?.Invoke(entry);
        }
        catch
        {
            // A misbehaving subscriber must never break logging
        }
    }

    /// <summary>
    /// Reads the tail of today's log file, if the file logging provider is enabled.
    /// </summary>
    public async Task<IReadOnlyList<LogEntry>> ReadHistoryAsync()
    {
        var fileProvider = settings.Value.Server.Logs.Providers
            .FirstOrDefault(p => p.Type == LoggingProviderType.File && p.Enabled);

        if (fileProvider == null)
            return [];

        var logFilePath = Path.Combine(fileProvider.ConnectionString, $"log-{DateTime.Now:yyyy-MM-dd}.txt");

        if (!File.Exists(logFilePath))
            return [];

        try
        {
            var lines = await ReadTailLinesAsync(logFilePath, MaxHistoryLines);

            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(ParseLogLine)
                .ToList();
        }
        catch
        {
            return [];
        }
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

    private static LogEntry ParseLogLine(string line)
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

            return new LogEntry(message, level, timestamp);
        }
        catch
        {
            return new LogEntry(line, LogLevel.Information, DateTime.Now);
        }
    }
}
