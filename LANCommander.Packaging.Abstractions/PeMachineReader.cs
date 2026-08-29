namespace LANCommander.Packaging;

/// <summary>
/// Machine type from a PE file's COFF header (IMAGE_FILE_MACHINE_*).
/// </summary>
public enum PeMachineType : ushort
{
    Unknown = 0x0000,
    I386 = 0x014C,
    Amd64 = 0x8664,
    Arm64 = 0xAA64,
    Arm = 0x01C0,
    ArmThumb2 = 0x01C4,
    IA64 = 0x0200,
}

/// <summary>
/// Reads the machine type out of a Windows PE image.
/// <para>
/// Pure managed parsing of the DOS stub, PE signature and COFF header — no P/Invoke — so it
/// works against files that are not currently executable and on any platform.
/// </para>
/// </summary>
/// <remarks>
/// Deliberately lives here rather than in LANCommander.PE: the packaging worker needs
/// architecture detection and is published self-contained for two architectures, so it cannot
/// afford to drag in that project's ImageSharp dependency for sixty lines of header parsing.
/// </remarks>
public static class PeMachineReader
{
    private const ushort DosSignature = 0x5A4D;   // "MZ"
    private const uint PeSignature = 0x00004550;  // "PE\0\0"
    private const int ELfanewOffset = 0x3C;

    /// <summary>
    /// Reads the machine type of the executable or library at <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// The machine type, or <see cref="PeMachineType.Unknown"/> when the file is missing,
    /// unreadable, or not a PE image.
    /// </returns>
    public static PeMachineType Read(string path)
    {
        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return Read(stream);
        }
        catch
        {
            // A locked, missing or truncated image is an expected condition when probing a live
            // process, so callers get Unknown rather than an exception to handle.
            return PeMachineType.Unknown;
        }
    }

    /// <summary>
    /// Reads the machine type from an already-open, seekable stream.
    /// </summary>
    public static PeMachineType Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek)
            throw new ArgumentException("A seekable stream is required.", nameof(stream));

        using var reader = new BinaryReader(stream, System.Text.Encoding.Default, leaveOpen: true);

        stream.Position = 0;

        if (stream.Length < ELfanewOffset + 4 || reader.ReadUInt16() != DosSignature)
            return PeMachineType.Unknown;

        stream.Position = ELfanewOffset;

        var peHeaderOffset = reader.ReadInt32();

        // The COFF header's Machine field is the first 2 bytes after the 4-byte PE signature.
        if (peHeaderOffset <= 0 || peHeaderOffset + 6 > stream.Length)
            return PeMachineType.Unknown;

        stream.Position = peHeaderOffset;

        if (reader.ReadUInt32() != PeSignature)
            return PeMachineType.Unknown;

        var machine = reader.ReadUInt16();

        return Enum.IsDefined(typeof(PeMachineType), machine)
            ? (PeMachineType)machine
            : PeMachineType.Unknown;
    }
}
