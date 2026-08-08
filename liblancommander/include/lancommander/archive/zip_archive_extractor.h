#ifndef LANCOMMANDER_ARCHIVE_ZIP_EXTRACTOR_H
#define LANCOMMANDER_ARCHIVE_ZIP_EXTRACTOR_H

#include "archive_extractor.h"

namespace lancommander {

// Zip extraction, built on the same libzip picoposh vendors — one zip
// implementation in the process rather than two.
//
// Every archive LANCommander serves is a plain zip: the server builds them
// with System.IO.Compression.ZipArchive throughout. The .NET SDK extracts via
// SharpCompress, which also reads rar/7z/tar, but nothing in LANCommander
// produces those.
//
// Availability follows picoposh's zip backend. When picoposh is built without
// its zlib/libzip submodules, `available()` returns false and both methods
// report cleanly rather than failing to link — the same shape as picoposh's
// own "not supported" stub.
class ZipArchiveExtractor : public IArchiveExtractor {
public:
    ZipArchiveExtractor();
    virtual ~ZipArchiveExtractor();

    // False when this build has no zip backend. Worth checking before
    // offering the user an install action that cannot succeed.
    static bool available();

    // Entries whose path escapes the destination — absolute paths, drive
    // letters, or any `..` component — are refused rather than written, since
    // an archive is untrusted input. A refused entry fails the extraction.
    ExtractionResult extract(
        const std::string& archive_path,
        const std::string& dest_directory,
        bool skip_existing_matching_crc = true,
        ExtractionProgressFn progress = ExtractionProgressFn());

    std::vector<ArchiveEntry> list(const std::string& archive_path);
};

} // namespace lancommander

#endif // LANCOMMANDER_ARCHIVE_ZIP_EXTRACTOR_H
