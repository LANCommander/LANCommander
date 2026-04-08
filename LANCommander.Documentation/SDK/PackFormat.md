# LANCommander Pack Format (`.lcp`)

## Overview

The LANCommander Pack (LCP) format is a custom binary container for distributing game files, patches, and metadata. It replaces the previous LCX format (YAML metadata + ZIP archive) with a purpose-built binary format that supports:

- **Pack identity and versioning** -- Each pack carries a unique ID and a freeform version string, enabling version tracking across game releases and patches.
- **Patch lineage** -- Patch packs reference their parent pack and the version they are based on, forming an ordered chain from base game through successive patches.
- **Per-entry integrity** -- Every file entry includes a CRC32 checksum. Section-level checksums cover the data and directory regions independently.
- **Per-entry compression** -- Each entry specifies its own compression method (None, Deflate, or ZStd), allowing already-compressed files (e.g., game archives) to skip double-compression.
- **Differential patching** -- Entries carry an operation field (Create, Modify, Delete), enabling patch packs that contain only what changed between versions.
- **Chunked transport** -- Large packs can be split into fixed-size chunks for transport, then transparently reassembled for extraction.
- **Forward-only extraction** -- The entry data region is designed for sequential reads. Extraction does not require seeking, enabling streaming from network sources.
- **Random-access directory** -- An optional directory section at the end of the file provides offset-based access to individual entries without scanning the data region.
- **Internal manifest** -- Rich metadata (game title, description, scripts, media references, dependencies) is stored as a manifest file *inside* the pack body, not in the binary header. The header stays lean and fixed-size for fast identification and validation.

---

## File Extension and Magic

| Item | Value |
|---|---|
| Extension | `.lcp` |
| Magic bytes | `LCPK` (4 bytes, ASCII) |
| Chunk magic | `LCPC` (4 bytes, ASCII) |

---

## Binary Layout

A pack file consists of four sections laid out sequentially:

```
+---------------------+
|       Header        |  112 bytes, fixed size
+---------------------+
|    Entry Headers     |  Variable, one per file
|    + File Data       |
|    (repeated)        |
+---------------------+
|  Directory Section   |  Optional, one entry per file
+---------------------+
|       Footer        |  28 bytes, fixed size
+---------------------+
```

All multi-byte integers are little-endian. Strings are UTF-8 unless otherwise noted.

---

## Header (112 bytes)

The header is fixed-size, enabling quick reads for identification, validation, and routing without parsing the pack body.

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 4 | Magic | `LCPK` (ASCII). Identifies the file as a LANCommander pack. |
| 4 | 2 | FormatVersion | Pack format version. Current value: `2`. Used for forward compatibility; readers should reject versions they do not understand. |
| 6 | 2 | Flags | Bit field. See [Flags](#flags). |
| 8 | 8 | EntryCount | Number of file entries in the data section (`uint64`). |
| 16 | 16 | PackId | GUID identifying this specific pack. Generated when the pack is created. Used to establish relationships between game packs and patch packs. |
| 32 | 16 | ParentPackId | GUID of the parent pack. For base game packs, this is `Guid.Empty` (all zeros). For patch packs, this references the base game pack's `PackId`, creating an identity link between related packs. |
| 48 | 32 | PackVersion | Freeform version string for this pack, UTF-8, null-padded to 32 bytes. Maximum 32 bytes of UTF-8 content. Not restricted to semantic versioning -- any format is valid (e.g., `1.0.0`, `Build 12345`, `2025.01.15`, `Gold`). |
| 80 | 32 | ParentVersion | Version string of the pack that this one follows, UTF-8, null-padded to 32 bytes. For base game packs, this is empty (all zeros). For patch packs, this is the version of the pack that must be installed before this patch can be applied -- either the base game version or the previous patch version. Forms a version chain for ordering. |

### Flags

| Bit | Name | Description |
|-----|------|-------------|
| 0 | HasDirectory | When set, the pack contains a directory section before the footer. Enables random-access reads. |
| 1-15 | Reserved | Must be zero. |

---

## Entry Header (variable size)

Each file in the pack is preceded by an entry header. Entry headers are written sequentially, interleaved with their file data.

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 4 | PathLength | Length of the `Path` field in bytes (`uint32`). |
| 4 | var | Path | Relative file path, UTF-8. Uses `/` as the separator regardless of platform. |
| 4+n | 1 | Operation | Entry operation. See [Operations](#operations). |
| 5+n | 1 | Compression | Compression method. See [Compression](#compression). |
| 6+n | 4 | Attributes | File attributes (`uint32`). Platform-specific file attribute flags. |
| 10+n | 8 | Timestamp | Last write time in UTC ticks (`int64`). |
| 18+n | 8 | UncompressedSize | Original file size in bytes (`uint64`). |
| 26+n | 8 | CompressedSize | Size of the data following this header (`uint64`). Equal to `UncompressedSize` when `Compression` is `None`. |
| 34+n | 4 | Checksum | CRC32 of the uncompressed file data (`uint32`). |

Immediately following the entry header is the file data (`CompressedSize` bytes). For `Delete` operations, `CompressedSize` is 0 and no data follows.

### Operations

| Value | Name | Description |
|-------|------|-------------|
| 0 | Create | New file. Used in base game packs and when a patch adds a file. |
| 1 | Modify | Modified file. Used in patch packs when a file changed between versions. |
| 2 | Delete | Deleted file. Header-only, no data follows. Used in patch packs when a file was removed. |

### Compression

| Value | Name | Description |
|-------|------|-------------|
| 0 | None | Uncompressed. Data is stored as-is. |
| 1 | Deflate | DEFLATE compression. |
| 2 | ZStd | Zstandard compression. |

Compression is per-entry, allowing mixed strategies within a single pack. Files that are already compressed (e.g., `.zip`, `.pak`, `.mp3`) should use `None` to avoid wasted CPU on negligible size reduction.

---

## Directory Section (optional, variable size)

When the `HasDirectory` flag is set, the directory section provides an index of all entries with their offsets into the data section. This enables random access to individual files without scanning the entire pack.

Each directory entry:

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 4 | PathLength | Length of the `Path` field in bytes (`uint32`). |
| 4 | var | Path | Relative file path, UTF-8. |
| 4+n | 1 | Operation | Entry operation (mirrors the entry header). |
| 5+n | 8 | Offset | Byte offset from the start of the file to the entry header (`uint64`). |
| 13+n | 8 | UncompressedSize | Original file size (`uint64`). |
| 21+n | 8 | CompressedSize | Compressed data size (`uint64`). |
| 29+n | 4 | Checksum | CRC32 of the uncompressed file data (`uint32`). |

---

## Footer (28 bytes)

The footer is at the very end of the file. Readers locate it by seeking to `EOF - 28`.

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 8 | DirectoryOffset | Byte offset from the start of the file to the directory section (`uint64`). Zero if no directory. |
| 8 | 8 | EntryCount | Number of entries (`uint64`). Mirrors the header for validation. |
| 16 | 4 | DataChecksum | CRC32 of the entire data section (all entry headers + file data, from byte 112 to directory start) (`uint32`). |
| 20 | 4 | DirectoryChecksum | CRC32 of the directory section (`uint32`). Zero if no directory. |
| 24 | 4 | Magic | `LCPK` (ASCII). Allows readers to confirm they found a valid footer. |

---

## Chunk Format

Packs larger than a configurable threshold (default: 4 GB - 1 byte) are split into numbered chunks for transport. The first chunk contains the pack data starting from byte 0 (including the pack header). Subsequent chunks are prefixed with a chunk header.

### Chunk file naming

| Chunk | Filename |
|-------|----------|
| 0 | `{name}.lcp` |
| 1 | `{name}.lcp.001` |
| 2 | `{name}.lcp.002` |
| ... | ... |

### Chunk Header (16 bytes)

Present on chunks 1+ only. Chunk 0 has no chunk header -- it starts directly with the pack header.

| Offset | Size | Field | Description |
|--------|------|-------|-------------|
| 0 | 4 | ChunkMagic | `LCPC` (ASCII). |
| 4 | 4 | ChunkIndex | Zero-based index of this chunk (`uint32`). |
| 8 | 4 | TotalChunks | Total number of chunks (`uint32`). |
| 12 | 4 | ParentCrc | CRC32 of the pack header (first 112 bytes of chunk 0) (`uint32`). Used to verify that all chunks belong to the same pack. |

To read a chunked pack, concatenate all chunks in order, skipping the 16-byte chunk header on chunks 1+. The result is a standard pack stream.

---

## Integrity Model

Integrity verification operates at three levels:

1. **Per-entry checksum** -- Each entry header contains a CRC32 of its uncompressed file data. Verified during extraction by computing CRC32 on the fly and comparing.

2. **Data section checksum** -- The footer's `DataChecksum` covers all bytes between the end of the header (byte 112) and the start of the directory section (or footer, if no directory). Verified by reading the region and computing CRC32.

3. **Directory section checksum** -- The footer's `DirectoryChecksum` covers all bytes in the directory section. Verified independently.

4. **Chunk binding** -- Each chunk header stores `ParentCrc`, a CRC32 of the pack header bytes. Verifies that chunks belong together without needing to read the full pack.

---

## Pack Relationships and Versioning

### Identity

Every pack has a `PackId` (GUID), generated at creation time. This uniquely identifies the pack.

### Parent link

Patch packs set `ParentPackId` to the base game pack's `PackId`. This creates a type-agnostic relationship: the system can find all patches for a game by matching `ParentPackId`. Base game packs set `ParentPackId` to `Guid.Empty`.

### Version chain

Versions are freeform strings (up to 32 bytes UTF-8). The `ParentVersion` field on a patch names the exact version it was built against.

Example chain:

```
Base game pack:
  PackId:        A1B2C3D4-...
  ParentPackId:  00000000-...
  PackVersion:   "1.0.0"
  ParentVersion: ""

Patch 1:
  PackId:        E5F6A7B8-...
  ParentPackId:  A1B2C3D4-...
  PackVersion:   "1.1.0"
  ParentVersion: "1.0.0"

Patch 2:
  PackId:        C9D0E1F2-...
  ParentPackId:  A1B2C3D4-...
  PackVersion:   "1.2.0"
  ParentVersion: "1.1.0"
```

This allows the system to:
- Determine patch order by following the `ParentVersion` chain.
- Validate that a patch is applicable to the currently installed version.
- Identify all packs in a game's lineage via `ParentPackId`.

---

## Differential Patching

Patch packs are created by diffing two pack manifests (directory sections):

1. **Read directories** of the old and new packs.
2. **Compare entries** by path:
   - Present in new but not old: `Create`
   - Present in both but checksums differ: `Modify`
   - Present in old but not new: `Delete`
3. **Build the patch pack** containing only the changed entries. `Create` and `Modify` entries carry their full file data from the new pack. `Delete` entries are header-only (no data).

Applying a patch is identical to normal extraction -- the entry `Operation` field drives behavior. `Create` and `Modify` write files, `Delete` removes them. The extraction engine requires no special patch logic.

---

## Extraction Behavior

| Option | Default | Description |
|--------|---------|-------------|
| VerifyChecksums | `true` | Verify per-entry CRC32 during extraction. |
| OverwriteExisting | `true` | Overwrite files that already exist on disk. |
| PreserveTimestamps | `true` | Restore original last-write timestamps after extraction. |
| SkipUnchangedFiles | `true` | Compare CRC32 of existing local files against the entry checksum. Skip extraction if they match. |

When `SkipUnchangedFiles` is enabled, the extractor computes CRC32 of the local file before reading the entry data. If checksums match, the entry data is skipped in the stream (seeked or drained), avoiding unnecessary I/O.

---

## Replacing LCX

The LCX format bundled a YAML metadata file with game archives, media, and scripts into a ZIP file. The pack format replaces this by:

1. Storing the metadata as a **manifest file inside the pack body** -- just another entry alongside game files. The manifest format (YAML or otherwise) is independent of the binary container format.
2. Storing game archives, media, scripts, and all other files as **pack entries** with individual checksums and optional compression.
3. Adding **version tracking** and **patch lineage** directly in the header, which LCX had no concept of.
4. Providing **integrity verification** at multiple levels, which ZIP's per-entry CRC32 only partially covered.

The pack header does not contain metadata fields like game title, description, or dependency lists. These belong in the manifest file inside the pack, keeping the binary format stable as metadata requirements evolve.
