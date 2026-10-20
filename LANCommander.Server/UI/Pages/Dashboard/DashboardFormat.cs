using System.Globalization;
using ByteSizeLib;

namespace LANCommander.Server.UI.Pages.Dashboard;

/// <summary>The dashboard's short figures: "6d 14h", "1h 08m", "1.24" + "TB of 4 TB".</summary>
public static class DashboardFormat
{
    /// <summary>A long span to the nearest hour once it passes a day: "6d 14h", "3h 05m", "12m".</summary>
    public static string Uptime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        if (time.TotalDays >= 1)
            return $"{(int)time.TotalDays}d {time.Hours}h";

        return Duration(time);
    }

    /// <summary>A session's length as the design writes it: "1h 08m", "46m".</summary>
    public static string Duration(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}h {time.Minutes:00}m"
            : $"{time.Minutes}m";
    }

    /// <summary>Total playtime in whole hours, or minutes under an hour: "41 h played", "25 m played".</summary>
    public static string HoursPlayed(TimeSpan time) =>
        time.TotalHours >= 1
            ? $"{Math.Floor(time.TotalHours).ToString("N0", CultureInfo.CurrentCulture)} h played"
            : $"{Math.Max(0, (int)time.TotalMinutes)} m played";

    /// <summary>A signed change with what it's measured against: "+5 vs 7d ago", "−2 vs 24h ago", "no change".</summary>
    public static string Delta(int delta, string against) => delta switch
    {
        > 0 => $"+{delta.ToString("N0", CultureInfo.CurrentCulture)} vs {against}",
        < 0 => $"−{Math.Abs(delta).ToString("N0", CultureInfo.CurrentCulture)} vs {against}",
        _ => $"no change vs {against}",
    };

    /// <summary>
    /// The storage tile's figure and its note: ("1.24", "TB of 4 TB"), or ("1.24", "TB used")
    /// without a capacity. Decimal units, so a 4 TB drive reads as 4 TB.
    /// </summary>
    public static (string Value, string Note) Storage(long usedBytes, long? capacityBytes)
    {
        var (usedValue, usedUnit) = Split(Math.Max(0, usedBytes));
        var usedFormat = usedUnit == ByteSize.ByteSymbol || usedValue >= 100 ? "0" : usedValue >= 10 ? "0.0" : "0.00";
        var value = usedValue.ToString(usedFormat, CultureInfo.CurrentCulture);

        if (capacityBytes is not > 0)
            return (value, $"{usedUnit} used");

        var (capacityValue, capacityUnit) = Split(capacityBytes.Value);
        var capacityFormat = capacityValue >= 100 ? "0" : capacityValue >= 10 ? "0.#" : "0.##";

        return (value, $"{usedUnit} of {capacityValue.ToString(capacityFormat, CultureInfo.CurrentCulture)} {capacityUnit}");
    }

    // ByteSize reports anything under a byte in bits ("0 b"); storage is never less than bytes
    static (double Value, string Unit) Split(long bytes)
    {
        if (bytes < 1000)
            return (bytes, ByteSize.ByteSymbol);

        var size = ByteSize.FromBytes(bytes);

        return (size.LargestWholeNumberDecimalValue, size.LargestWholeNumberDecimalSymbol);
    }

    /// <summary>Shortens a category label so columns don't collide: "Natural Selection 2" → "Natural Sel…".</summary>
    public static string Abbreviate(string label, int max = 12) =>
        String.IsNullOrEmpty(label) || label.Length <= max ? label : label[..(max - 1)].TrimEnd() + "…";
}
