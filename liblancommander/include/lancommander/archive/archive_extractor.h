#ifndef LANCOMMANDER_ARCHIVE_EXTRACTOR_H
#define LANCOMMANDER_ARCHIVE_EXTRACTOR_H

#include <string>
#include <vector>
#include <functional>

namespace lancommander {

// Information about a single entry in an archive.
struct ArchiveEntry {
    std::string path;          // Relative path within the archive
    bool is_directory;         // True if this entry is a directory
    unsigned long crc32;       // CRC32 checksum (0 if unavailable)
    long long compressed_size;
    long long uncompressed_size;
};

// Result of an extraction operation.
struct ExtractionResult {
    bool success;
    bool canceled;
    std::string directory;     // Destination directory on success
    std::string error;         // Error message on failure
    std::vector<std::string> extracted_files;  // Paths of files actually written
};

// Progress callback for extraction.
//   entries_done  — number of entries processed so far
//   entries_total — total entries in the archive (0 if unknown)
//   bytes_done    — bytes written so far
//   bytes_total   — total uncompressed size (0 if unknown)
// Return false to cancel extraction.
using ExtractionProgressFn = std::function<bool(
    int entries_done, int entries_total,
    long long bytes_done, long long bytes_total)>;

// Abstract interface for archive extraction.
//
// Platform-specific implementations should be provided behind #ifdef guards
// or as separate compilation units. Possible backends:
//   - minizip/zlib (broad compatibility, including Win9x with older compilers)
//   - libarchive   (modern systems with wide format support)
//   - platform-native APIs
//
// The interface is intentionally minimal — it covers the extraction path
// needed by ToolClient and GameClient. Archive creation (for save packing)
// can be added as a separate interface when needed.
class IArchiveExtractor {
public:
    virtual ~IArchiveExtractor() {}

    // Extract an archive file to a destination directory.
    // If skip_existing_matching_crc is true, files whose on-disk CRC32 matches
    // the archive entry's CRC32 are skipped (mirrors the C# SDK behavior).
    virtual ExtractionResult extract(
        const std::string& archive_path,
        const std::string& dest_directory,
        bool skip_existing_matching_crc = true,
        ExtractionProgressFn progress = ExtractionProgressFn()) = 0;

    // List entries in an archive without extracting.
    virtual std::vector<ArchiveEntry> list(const std::string& archive_path) = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_ARCHIVE_EXTRACTOR_H
