using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LANCommander.Server.Plugins.Data;

/// <summary>
/// Converters that make types behave the same on every engine. The host applies
/// <see cref="UtcDateTime"/> and <see cref="UtcTicks"/> to every <see cref="DateTime"/> and
/// <see cref="DateTimeOffset"/> property itself; a <see cref="decimal"/> property needs
/// <see cref="ScaledInteger"/> applied by the plugin, since only the plugin knows its scale.
/// </summary>
public static class PluginValueConverters
{
    /// <summary>
    /// Stores <see cref="DateTime"/> as UTC, which PostgreSQL requires. Local times are converted;
    /// unspecified ones are taken to be UTC already. Values read back are marked UTC.
    /// </summary>
    public static ValueConverter<DateTime, DateTime> UtcDateTime { get; } = new(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// Stores <see cref="DateTimeOffset"/> as its UTC ticks, which every engine can compare and
    /// order. The offset isn't kept: values read back are in UTC.
    /// </summary>
    public static ValueConverter<DateTimeOffset, long> UtcTicks { get; } = new(
        value => value.UtcTicks,
        value => new DateTimeOffset(value, TimeSpan.Zero));

    /// <summary>
    /// Stores <see cref="decimal"/> as a whole number of 10^-<paramref name="scale"/> units, which every
    /// engine can compare and order; SQLite otherwise stores decimals as text. Values are rounded
    /// to <paramref name="scale"/> places.
    /// </summary>
    public static ValueConverter<decimal, long> ScaledInteger(int scale)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scale);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(scale, 18);

        var factor = (decimal)Math.Pow(10, scale);

        return new ValueConverter<decimal, long>(
            value => (long)decimal.Round(value * factor, MidpointRounding.AwayFromZero),
            value => value / factor);
    }
}
