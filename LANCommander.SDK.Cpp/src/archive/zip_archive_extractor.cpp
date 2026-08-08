#include "lancommander/archive/zip_archive_extractor.h"

#include "lancommander/archive/crc32_util.h"
#include "lancommander/util/path.h"

#ifdef LANCOMMANDER_HAVE_LIBZIP
#include <zip.h>
#endif

#include <cstdio>
#include <string>
#include <vector>

namespace lancommander {

namespace {

// Rejects anything that would let an entry escape the destination directory:
// an absolute path, a drive-qualified path, or any `..` component. Archives
// are untrusted input, so this is a hard failure rather than a sanitisation.
bool is_safe_entry_path(const std::string& name)
{
    if (name.empty())
        return false;

    if (name[0] == '/' || name[0] == '\\')
        return false;

    // "C:..." or "C:/..."
    if (name.size() >= 2 && name[1] == ':')
        return false;

    std::size_t start = 0;
    while (start <= name.size()) {
        std::size_t end = name.find_first_of("/\\", start);
        const bool last = (end == std::string::npos);
        if (last)
            end = name.size();

        const std::string component = name.substr(start, end - start);
        if (component == "..")
            return false;

        if (last)
            break;
        start = end + 1;
    }

    return true;
}

// Archive entries always use '/'; the destination wants the platform's.
std::string to_native(const std::string& name)
{
    std::string out = name;
    for (std::size_t i = 0; i < out.size(); ++i) {
        if (out[i] == '/' || out[i] == '\\')
            out[i] = path::separator();
    }
    return out;
}

bool ends_with_separator(const std::string& name)
{
    return !name.empty() &&
           (name[name.size() - 1] == '/' || name[name.size() - 1] == '\\');
}

} // namespace

ZipArchiveExtractor::ZipArchiveExtractor() {}
ZipArchiveExtractor::~ZipArchiveExtractor() {}

#ifdef LANCOMMANDER_HAVE_LIBZIP

bool ZipArchiveExtractor::available() { return true; }

ExtractionResult ZipArchiveExtractor::extract(
    const std::string& archive_path,
    const std::string& dest_directory,
    bool skip_existing_matching_crc,
    ExtractionProgressFn progress)
{
    ExtractionResult result;
    result.success = false;
    result.canceled = false;
    result.directory = dest_directory;

    int error_code = 0;
    zip_t* archive = zip_open(archive_path.c_str(), ZIP_RDONLY, &error_code);
    if (!archive) {
        result.error = "could not open archive: " + archive_path;
        return result;
    }

    Result<bool> made = path::create_directories(dest_directory);
    if (!made) {
        zip_close(archive);
        result.error = made.error;
        return result;
    }

    const zip_int64_t count = zip_get_num_entries(archive, 0);

    // Totals up front so progress can report a percentage.
    long long bytes_total = 0;
    for (zip_int64_t i = 0; i < count; ++i) {
        zip_stat_t stat;
        if (zip_stat_index(archive, i, 0, &stat) == 0 &&
            (stat.valid & ZIP_STAT_SIZE))
            bytes_total += (long long)stat.size;
    }

    long long bytes_done = 0;
    std::vector<char> buffer(64 * 1024);

    for (zip_int64_t i = 0; i < count; ++i) {
        zip_stat_t stat;
        if (zip_stat_index(archive, i, 0, &stat) != 0)
            continue;

        const std::string name = stat.name ? stat.name : "";

        if (!is_safe_entry_path(name)) {
            zip_close(archive);
            result.error = "archive contains an unsafe entry path: " + name;
            return result;
        }

        const std::string destination =
            path::combine(dest_directory, to_native(name));

        if (ends_with_separator(name)) {
            path::create_directories(destination);
            continue;
        }

        Result<bool> parent = path::create_directories(path::parent(destination));
        if (!parent) {
            zip_close(archive);
            result.error = parent.error;
            return result;
        }

        // Skipping unchanged files is what makes reinstalling over an existing
        // directory cheap, and it is what the .NET SDK does.
        if (skip_existing_matching_crc && (stat.valid & ZIP_STAT_CRC) &&
            path::exists(destination)) {
            if (Crc32::file_crc32(destination.c_str()) == (unsigned long)stat.crc) {
                bytes_done += (stat.valid & ZIP_STAT_SIZE) ? (long long)stat.size : 0;
                if (progress && !progress((int)(i + 1), (int)count,
                                          bytes_done, bytes_total)) {
                    zip_close(archive);
                    result.canceled = true;
                    return result;
                }
                continue;
            }
        }

        zip_file_t* entry = zip_fopen_index(archive, i, 0);
        if (!entry) {
            zip_close(archive);
            result.error = "could not read archive entry: " + name;
            return result;
        }

        std::FILE* out = std::fopen(destination.c_str(), "wb");
        if (!out) {
            zip_fclose(entry);
            zip_close(archive);
            result.error = "could not write " + destination;
            return result;
        }

        bool failed = false;
        for (;;) {
            const zip_int64_t read =
                zip_fread(entry, &buffer[0], (zip_uint64_t)buffer.size());
            if (read < 0) {
                failed = true;
                break;
            }
            if (read == 0)
                break;

            if (std::fwrite(&buffer[0], 1, (std::size_t)read, out) !=
                (std::size_t)read) {
                failed = true;
                break;
            }

            bytes_done += (long long)read;
        }

        std::fclose(out);
        zip_fclose(entry);

        if (failed) {
            zip_close(archive);
            result.error = "failed while extracting " + name;
            return result;
        }

        result.extracted_files.push_back(destination);

        if (progress && !progress((int)(i + 1), (int)count,
                                  bytes_done, bytes_total)) {
            zip_close(archive);
            result.canceled = true;
            return result;
        }
    }

    zip_close(archive);

    result.success = true;
    return result;
}

std::vector<ArchiveEntry> ZipArchiveExtractor::list(const std::string& archive_path)
{
    std::vector<ArchiveEntry> entries;

    int error_code = 0;
    zip_t* archive = zip_open(archive_path.c_str(), ZIP_RDONLY, &error_code);
    if (!archive)
        return entries;

    const zip_int64_t count = zip_get_num_entries(archive, 0);

    for (zip_int64_t i = 0; i < count; ++i) {
        zip_stat_t stat;
        if (zip_stat_index(archive, i, 0, &stat) != 0)
            continue;

        ArchiveEntry entry;
        entry.path = stat.name ? stat.name : "";
        entry.is_directory = ends_with_separator(entry.path);
        entry.crc32 = (stat.valid & ZIP_STAT_CRC) ? (unsigned long)stat.crc : 0;
        entry.compressed_size =
            (stat.valid & ZIP_STAT_COMP_SIZE) ? (long long)stat.comp_size : 0;
        entry.uncompressed_size =
            (stat.valid & ZIP_STAT_SIZE) ? (long long)stat.size : 0;

        entries.push_back(entry);
    }

    zip_close(archive);

    return entries;
}

#else // LANCOMMANDER_HAVE_LIBZIP

bool ZipArchiveExtractor::available() { return false; }

ExtractionResult ZipArchiveExtractor::extract(
    const std::string& archive_path,
    const std::string& dest_directory,
    bool skip_existing_matching_crc,
    ExtractionProgressFn progress)
{
    (void)archive_path;
    (void)skip_existing_matching_crc;
    (void)progress;

    ExtractionResult result;
    result.success = false;
    result.canceled = false;
    result.directory = dest_directory;
    result.error = "this build has no zip backend: picoposh was built without "
                   "its zlib and libzip submodules";
    return result;
}

std::vector<ArchiveEntry> ZipArchiveExtractor::list(const std::string& archive_path)
{
    (void)archive_path;
    return std::vector<ArchiveEntry>();
}

#endif // LANCOMMANDER_HAVE_LIBZIP

} // namespace lancommander
