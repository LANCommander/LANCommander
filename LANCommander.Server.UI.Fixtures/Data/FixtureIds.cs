using System.Security.Cryptography;
using System.Text;

namespace LANCommander.Server.UI.Fixtures.Data;

/// <summary>
/// Stable identifiers and timestamps for seeded data, so every seeded database is identical and
/// captures of it can be compared pixel for pixel.
/// </summary>
public static class FixtureIds
{
    /// <summary>The moment fixture data is seeded "at". Visual tests pin the browser clock here too.</summary>
    public static readonly DateTime Epoch = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>A Guid derived from <paramref name="key"/>; the same key always yields the same Guid.</summary>
    public static Guid For(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("lancommander-fixture:" + key));

        return new Guid(hash);
    }

    /// <summary>
    /// A creation time before <see cref="Epoch"/> derived from an entity's Id, spreading seeded
    /// entities over the preceding year without depending on the order they were written in.
    /// </summary>
    public static DateTime CreatedOn(Guid id)
    {
        var bytes = id.ToByteArray();
        var minutes = BitConverter.ToUInt32(bytes, 0) % (365 * 24 * 60);

        return Epoch.AddMinutes(-(double)minutes - 60);
    }

    /// <summary>An update time between <see cref="CreatedOn"/> and <see cref="Epoch"/>.</summary>
    public static DateTime UpdatedOn(Guid id)
    {
        var created = CreatedOn(id);
        var bytes = id.ToByteArray();
        var span = (Epoch - created).TotalMinutes;

        return created.AddMinutes(BitConverter.ToUInt32(bytes, 4) % Math.Max(1, (uint)span));
    }
}
