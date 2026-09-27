using LANCommander.Server.Data.Models;

namespace LANCommander.Server.UI.Pages.Dashboard.Charts;

/// <summary>A labelled amount of playtime, as the dashboard charts plot it.</summary>
public sealed record Playtime(string Label, TimeSpan Time)
{
    public double Seconds => Time.TotalSeconds;

    /// <summary>"4h 30m", or "45m" under an hour.</summary>
    public static string Format(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);

        return time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}m" : $"{time.Minutes}m";
    }

    /// <summary>The total length of the sessions that have both a start and an end.</summary>
    public static TimeSpan Total(IEnumerable<PlaySession> sessions) =>
        sessions
            .Where(s => s.Start != null && s.End != null)
            .Aggregate(TimeSpan.Zero, (total, s) => total + (s.End!.Value - s.Start!.Value));
}
