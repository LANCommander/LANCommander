using System.Text;

namespace LANCommander.Server.Tests.Helpers;

/// <summary>
/// Manufactures minimal, valid PE (Portable Executable) binary data entirely in memory,
/// so tests are self-contained and do not rely on any real binary being present on disk.
/// </summary>
/// <remarks>
/// The generated PE has a single .rsrc section with exactly the icon resources provided.
///
/// Resource directory layout (3 levels, per the Windows PE spec):
///   Level 0 – Root directory        keyed by resource TYPE (RT_ICON=3, RT_GROUP_ICON=14)
///   Level 1 – Type directory        keyed by resource NAME or numeric ID
///   Level 2 – Language directory    keyed by language ID; entries point directly to IMAGE_RESOURCE_DATA_ENTRYs
///
/// Writing the bytes by hand rather than with AsmResolver keeps the fixtures independent of the library
/// PEIconExtractor reads them back with, so a bug in that library cannot cancel itself out in the tests.
/// </remarks>
internal static class PEBuilder
{
    private const int RT_ICON       = 3;
    private const int RT_GROUP_ICON = 14;

    private const uint SectionVA      = 0x1000;
    private const uint FileAlignment  = 0x200;
    private const uint SectionAlign   = 0x1000;
    private const uint ImageBase      = 0x00400000;
    private const ushort LangIdEnUs   = 1033;

    // -------------------------------------------------------------------------
    //  Public entry point
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds a minimal PE binary containing icon resources described by <paramref name="groups"/>
    /// and the individual RT_ICON data supplied in <paramref name="iconData"/>.
    /// </summary>
    /// <param name="iconData">RT_ICON resources: resource ID → raw bytes (DIB or PNG).</param>
    /// <param name="groups">
    ///   Icon groups: each group has an ID and a list of images.
    ///   Each image is (iconResourceId, displayWidth, displayHeight, bitCount).
    /// </param>
    public static byte[] Build(
        Dictionary<int, byte[]> iconData,
        List<(int GroupId, List<(int IconId, byte Width, byte Height, ushort BitCount)> Images)> groups)
    {
        var groupIconData = new Dictionary<int, byte[]>();
        foreach (var (groupId, images) in groups)
            groupIconData[groupId] = BuildGroupIconDir(images, iconData);

        var allResources = new Dictionary<int, Dictionary<int, byte[]>>
        {
            [RT_ICON]       = iconData,
            [RT_GROUP_ICON] = groupIconData,
        };

        var (rsrcBytes, virtualSize) = BuildResourceSection(allResources);
        return BuildPe(rsrcBytes, virtualSize);
    }

    /// <summary>
    /// Builds a PE with no resource section at all — resourceRVA will be zero.
    /// PEIconExtractor should return an empty icon list for such a binary.
    /// </summary>
    public static byte[] BuildWithoutResources()
    {
        return BuildPe([], 0);
    }

    // -------------------------------------------------------------------------
    //  GRPICONDIR construction
    // -------------------------------------------------------------------------

    private static byte[] BuildGroupIconDir(
        List<(int IconId, byte Width, byte Height, ushort BitCount)> images,
        Dictionary<int, byte[]> iconData)
    {
        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms);

        // GRPICONDIR header
        w.Write((ushort)0);                  // Reserved
        w.Write((ushort)1);                  // Type = icon
        w.Write((ushort)images.Count);       // Count

        // GRPICONDIRENTRY per image
        foreach (var (iconId, width, height, bitCount) in images)
        {
            uint size = iconData.TryGetValue(iconId, out var d) ? (uint)d.Length : 0u;
            byte cc   = bitCount <= 8 ? (byte)(1 << bitCount) : (byte)0;

            w.Write(width);            // Width
            w.Write(height);           // Height
            w.Write(cc);               // ColorCount
            w.Write((byte)0);          // Reserved
            w.Write((ushort)1);        // Planes
            w.Write(bitCount);         // BitCount
            w.Write(size);             // BytesInRes
            w.Write((ushort)iconId);   // Id (references RT_ICON resource)
        }

        return ms.ToArray();
    }

    // -------------------------------------------------------------------------
    //  Resource section construction
    // -------------------------------------------------------------------------

    private static (byte[] Data, uint VirtualSize) BuildResourceSection(
        Dictionary<int, Dictionary<int, byte[]>> resources)
    {
        // ── Layout calculation ────────────────────────────────────────────────

        // Types are sorted numerically for stable layout
        var types = resources.Keys.OrderBy(t => t).ToList();

        // Total number of individual resources across all types
        int totalResources = resources.Values.Sum(d => d.Count);

        // Directory block sizes (all section-relative offsets):
        //   Root dir:      16 + types.Count * 8
        //   Per-type dirs: 16 + ids.Count * 8  (one per type)
        //   Per-ID dirs:   24 each              (one per resource — these are the language dirs)
        //   Data entries:  16 each
        int rootDirSize   = 16 + types.Count * 8;
        int typeDirsSize  = types.Sum(t => 16 + resources[t].Count * 8);
        int langDirsSize  = totalResources * (16 + 1 * 8); // each lang dir has exactly 1 entry
        int dataEntrySize = totalResources * 16;

        int dirBlockSize = rootDirSize + typeDirsSize + langDirsSize + dataEntrySize;

        // Pre-assign section-relative offsets for every directory and data entry
        var typeDirOffsets = new Dictionary<int, int>();
        int cur = rootDirSize;
        foreach (var t in types)
        {
            typeDirOffsets[t] = cur;
            cur += 16 + resources[t].Count * 8;
        }

        var langDirOffsets  = new Dictionary<(int type, int id), int>();
        var dataEntryOffsets = new Dictionary<(int type, int id), int>();

        foreach (var t in types)
        foreach (var id in resources[t].Keys.OrderBy(x => x))
        {
            langDirOffsets[(t, id)]  = cur;  cur += 24;
        }
        int dataEntryStart = cur;
        foreach (var t in types)
        foreach (var id in resources[t].Keys.OrderBy(x => x))
        {
            dataEntryOffsets[(t, id)] = cur;  cur += 16;
        }

        // Raw resource data follows the directory block, aligned to 4 bytes
        var rawOffsets = new Dictionary<(int type, int id), int>();
        var rawSizes   = new Dictionary<(int type, int id), int>();
        int rawCur = dirBlockSize;
        foreach (var t in types)
        foreach (var (id, bytes) in resources[t].OrderBy(x => x.Key))
        {
            rawOffsets[(t, id)] = rawCur;
            rawSizes[(t, id)]   = bytes.Length;
            rawCur += bytes.Length;
            rawCur  = (rawCur + 3) & ~3; // align to 4 bytes
        }

        uint virtualSize = (uint)rawCur;

        // ── Writing ───────────────────────────────────────────────────────────

        var buf = new byte[rawCur];
        using var ms = new MemoryStream(buf);
        using var w  = new BinaryWriter(ms);

        // Root directory
        Seek(ms, 0);
        WriteDir(w, types.Count);
        foreach (var t in types)
        {
            w.Write((uint)t);
            w.Write(0x80000000u | (uint)typeDirOffsets[t]);  // subdir bit
        }

        // Type directories (level 1)
        foreach (var t in types)
        {
            Seek(ms, typeDirOffsets[t]);
            var ids = resources[t].Keys.OrderBy(x => x).ToList();
            WriteDir(w, ids.Count);
            foreach (var id in ids)
            {
                w.Write((uint)id);
                w.Write(0x80000000u | (uint)langDirOffsets[(t, id)]);  // subdir bit → lang dir
            }
        }

        // Language directories (level 2) — each holds one entry pointing to a data entry
        foreach (var t in types)
        foreach (var id in resources[t].Keys.OrderBy(x => x))
        {
            Seek(ms, langDirOffsets[(t, id)]);
            WriteDir(w, 1);
            w.Write((uint)LangIdEnUs);
            w.Write((uint)dataEntryOffsets[(t, id)]);  // NO subdir bit — points to data entry
        }

        // Data entries (IMAGE_RESOURCE_DATA_ENTRY)
        foreach (var t in types)
        foreach (var id in resources[t].Keys.OrderBy(x => x))
        {
            Seek(ms, dataEntryOffsets[(t, id)]);
            uint dataRVA = SectionVA + (uint)rawOffsets[(t, id)];
            w.Write(dataRVA);                       // OffsetToData (absolute RVA)
            w.Write((uint)rawSizes[(t, id)]);       // Size
            w.Write((uint)0);                       // CodePage
            w.Write((uint)0);                       // Reserved
        }

        // Raw resource data
        foreach (var t in types)
        foreach (var (id, bytes) in resources[t].OrderBy(x => x.Key))
        {
            Seek(ms, rawOffsets[(t, id)]);
            w.Write(bytes);
        }

        return (buf, virtualSize);
    }

    // Writes the 16-byte IMAGE_RESOURCE_DIRECTORY header with idCount ID entries and 0 named entries.
    private static void WriteDir(BinaryWriter w, int idCount)
    {
        w.Write((uint)0);          // Characteristics
        w.Write((uint)0);          // TimeDateStamp
        w.Write((ushort)0);        // MajorVersion
        w.Write((ushort)0);        // MinorVersion
        w.Write((ushort)0);        // NumberOfNamedEntries
        w.Write((ushort)idCount);  // NumberOfIdEntries
    }

    private static void Seek(MemoryStream ms, int offset) => ms.Position = offset;

    // -------------------------------------------------------------------------
    //  PE file construction
    // -------------------------------------------------------------------------

    // File layout:
    //   0x0000: DOS header (64 bytes, e_lfanew → 0x40)
    //   0x0040: PE signature "PE\0\0" (4 bytes)
    //   0x0044: COFF header (20 bytes)
    //   0x0058: Optional header PE32 (224 bytes)
    //   0x0138: Section header × 1 (40 bytes)
    //   0x0160: Padding to FileAlignment (0x200)
    //   0x0200: .rsrc section raw data
    //
    // (If resourceSectionSize == 0, the PE has zero sections and no resource data directory entry.)
    private static byte[] BuildPe(byte[] rsrcSection, uint virtualSize)
    {
        bool hasResources = virtualSize > 0;

        uint rsrcRawSize    = hasResources ? AlignTo(virtualSize, FileAlignment) : 0;
        uint sizeOfHeaders  = 0x200;
        uint sizeOfImage    = hasResources
            ? AlignTo(SectionVA + virtualSize, SectionAlign)
            : AlignTo(sizeOfHeaders,           SectionAlign);

        ushort sectionCount = hasResources ? (ushort)1 : (ushort)0;
        uint   rsrcOffset   = 0x200;

        var buf = new byte[rsrcOffset + rsrcRawSize];
        using var ms = new MemoryStream(buf);
        using var w  = new BinaryWriter(ms);

        // ── DOS Header ────────────────────────────────────────────────────────
        ms.Position = 0;
        w.Write((ushort)0x5A4D);                          // MZ
        for (int i = 0; i < 29; i++) w.Write((ushort)0); // filler
        w.Write((uint)0x40);                              // e_lfanew

        // ── PE Signature ──────────────────────────────────────────────────────
        ms.Position = 0x40;
        w.Write((uint)0x00004550); // "PE\0\0"

        // ── COFF Header (20 bytes) ────────────────────────────────────────────
        w.Write((ushort)0x014C);     // Machine = x86
        w.Write(sectionCount);       // NumberOfSections
        w.Write((uint)0);            // TimeDateStamp
        w.Write((uint)0);            // PointerToSymbolTable
        w.Write((uint)0);            // NumberOfSymbols
        w.Write((ushort)224);        // SizeOfOptionalHeader (PE32)
        w.Write((ushort)0x0002);     // Characteristics = executable

        // ── Optional Header (PE32, 224 bytes) ─────────────────────────────────
        // Standard fields (28 bytes)
        w.Write((ushort)0x010B);   // Magic = PE32
        w.Write((byte)0);          // MajorLinkerVersion
        w.Write((byte)0);          // MinorLinkerVersion
        w.Write((uint)0);          // SizeOfCode
        w.Write((uint)rsrcRawSize);// SizeOfInitializedData
        w.Write((uint)0);          // SizeOfUninitializedData
        w.Write((uint)0);          // AddressOfEntryPoint
        w.Write((uint)0);          // BaseOfCode
        w.Write((uint)0);          // BaseOfData  (PE32 only)

        // Windows-specific fields (68 bytes)
        w.Write(ImageBase);          // ImageBase
        w.Write(SectionAlign);       // SectionAlignment
        w.Write(FileAlignment);      // FileAlignment
        w.Write((ushort)4);          // MajorOperatingSystemVersion
        w.Write((ushort)0);          // MinorOperatingSystemVersion
        w.Write((ushort)0);          // MajorImageVersion
        w.Write((ushort)0);          // MinorImageVersion
        w.Write((ushort)4);          // MajorSubsystemVersion
        w.Write((ushort)0);          // MinorSubsystemVersion
        w.Write((uint)0);            // Win32VersionValue
        w.Write(sizeOfImage);        // SizeOfImage
        w.Write(sizeOfHeaders);      // SizeOfHeaders
        w.Write((uint)0);            // CheckSum
        w.Write((ushort)2);          // Subsystem = Windows GUI
        w.Write((ushort)0);          // DllCharacteristics
        w.Write((uint)0x100000);     // SizeOfStackReserve
        w.Write((uint)0x1000);       // SizeOfStackCommit
        w.Write((uint)0x100000);     // SizeOfHeapReserve
        w.Write((uint)0x1000);       // SizeOfHeapCommit
        w.Write((uint)0);            // LoaderFlags
        w.Write((uint)16);           // NumberOfRvaAndSizes

        // Data directories (16 × 8 = 128 bytes)
        w.Write((uint)0); w.Write((uint)0); // [0] Export
        w.Write((uint)0); w.Write((uint)0); // [1] Import
        if (hasResources) { w.Write(SectionVA); w.Write(virtualSize); } // [2] Resource
        else              { w.Write((uint)0);   w.Write((uint)0);      }
        for (int i = 3; i < 16; i++) { w.Write((uint)0); w.Write((uint)0); }

        // ── Section Header (.rsrc) ─────────────────────────────────────────────
        // Only emitted when there is resource data.
        if (hasResources)
        {
            // ms.Position should be 0x0138 here (88 + 224 = 312 = 0x138)
            w.Write(Encoding.ASCII.GetBytes(".rsrc\0\0\0")); // Name (8 bytes)
            w.Write(virtualSize);    // VirtualSize
            w.Write(SectionVA);      // VirtualAddress
            w.Write(rsrcRawSize);    // SizeOfRawData
            w.Write(rsrcOffset);     // PointerToRawData
            w.Write((uint)0);        // PointerToRelocations
            w.Write((uint)0);        // PointerToLinenumbers
            w.Write((ushort)0);      // NumberOfRelocations
            w.Write((ushort)0);      // NumberOfLinenumbers
            w.Write((uint)0x40000040); // Characteristics

            // .rsrc section data at file offset 0x200
            ms.Position = rsrcOffset;
            w.Write(rsrcSection);
        }

        return buf;
    }

    private static uint AlignTo(uint value, uint align) => (value + align - 1) & ~(align - 1);
}
