#include "test_main.h"

#include "lancommander/archive/crc32_util.h"
#include "lancommander/archive/zip_archive_extractor.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>
#include <vector>

using namespace lancommander;

namespace {

// A minimal stored-entry zip writer, so the extractor is tested against real
// archive bytes rather than a mock. Stored (method 0) keeps this short while
// still exercising CRCs, the central directory and path handling.
struct ZipBuilder {
    struct Entry {
        std::string name;
        std::string data;
        unsigned long crc;
        std::size_t local_offset;
    };

    std::vector<Entry> entries;

    void add(const std::string& name, const std::string& data)
    {
        Entry e;
        e.name = name;
        e.data = data;

        Crc32 crc;
        if (!data.empty())
            crc.update(data.data(), data.size());
        e.crc = data.empty() ? 0 : crc.value();
        e.local_offset = 0;

        entries.push_back(e);
    }

    static void put16(std::string* out, unsigned int v)
    {
        out->push_back((char)(v & 0xFF));
        out->push_back((char)((v >> 8) & 0xFF));
    }

    static void put32(std::string* out, unsigned long v)
    {
        out->push_back((char)(v & 0xFF));
        out->push_back((char)((v >> 8) & 0xFF));
        out->push_back((char)((v >> 16) & 0xFF));
        out->push_back((char)((v >> 24) & 0xFF));
    }

    std::string build()
    {
        std::string out;

        for (std::size_t i = 0; i < entries.size(); ++i) {
            Entry& e = entries[i];
            e.local_offset = out.size();

            put32(&out, 0x04034b50);              // local file header
            put16(&out, 20);                      // version needed
            put16(&out, 0);                       // flags
            put16(&out, 0);                       // method: stored
            put16(&out, 0);                       // mod time
            put16(&out, 0);                       // mod date
            put32(&out, e.crc);
            put32(&out, (unsigned long)e.data.size());
            put32(&out, (unsigned long)e.data.size());
            put16(&out, (unsigned int)e.name.size());
            put16(&out, 0);                       // extra length
            out += e.name;
            out += e.data;
        }

        const std::size_t central_offset = out.size();

        for (std::size_t i = 0; i < entries.size(); ++i) {
            const Entry& e = entries[i];

            put32(&out, 0x02014b50);              // central directory header
            put16(&out, 20);                      // version made by
            put16(&out, 20);                      // version needed
            put16(&out, 0);                       // flags
            put16(&out, 0);                       // method
            put16(&out, 0);                       // mod time
            put16(&out, 0);                       // mod date
            put32(&out, e.crc);
            put32(&out, (unsigned long)e.data.size());
            put32(&out, (unsigned long)e.data.size());
            put16(&out, (unsigned int)e.name.size());
            put16(&out, 0);                       // extra
            put16(&out, 0);                       // comment
            put16(&out, 0);                       // disk number
            put16(&out, 0);                       // internal attrs
            put32(&out, 0);                       // external attrs
            put32(&out, (unsigned long)e.local_offset);
            out += e.name;
        }

        const std::size_t central_size = out.size() - central_offset;

        put32(&out, 0x06054b50);                  // end of central directory
        put16(&out, 0);                           // this disk
        put16(&out, 0);                           // disk with central dir
        put16(&out, (unsigned int)entries.size());
        put16(&out, (unsigned int)entries.size());
        put32(&out, (unsigned long)central_size);
        put32(&out, (unsigned long)central_offset);
        put16(&out, 0);                           // comment length

        return out;
    }
};

bool write_file(const std::string& path, const std::string& contents)
{
    path::create_directories(path::parent(path));

    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;
    if (!contents.empty())
        std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);
    return true;
}

std::string read_file(const std::string& path)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return std::string();

    std::string out;
    char buffer[1024];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out.append(buffer, read);
    std::fclose(file);
    return out;
}

std::string scratch(const std::string& leaf)
{
    return path::combine(path::combine(path::temp_directory(), "lc_archive_test"),
                         leaf);
}

} // namespace

void test_archive()
{
    if (!ZipArchiveExtractor::available()) {
        std::printf("  (skipping archive tests: this build has no zip backend)\n");
        return;
    }

    const std::string archive = scratch("sample.zip");
    const std::string destination = scratch("out");

    // --- a normal archive ---------------------------------------------------
    {
        ZipBuilder builder;
        builder.add("readme.txt", "hello world");
        builder.add("data/nested.txt", "nested payload");
        CHECK(write_file(archive, builder.build()));

        ZipArchiveExtractor extractor;
        ExtractionResult result = extractor.extract(archive, destination, false);

        CHECK(result.success);
        CHECK(!result.canceled);
        CHECK(result.extracted_files.size() == 2);
        CHECK_EQ(read_file(path::combine(destination, "readme.txt")), "hello world");
        CHECK_EQ(read_file(path::combine(destination,
                                         path::combine("data", "nested.txt"))),
                 "nested payload");
    }

    // --- list() ------------------------------------------------------------
    {
        ZipArchiveExtractor extractor;
        std::vector<ArchiveEntry> entries = extractor.list(archive);

        CHECK(entries.size() == 2);
        if (entries.size() == 2) {
            CHECK_EQ(entries[0].path, "readme.txt");
            CHECK(entries[0].uncompressed_size == 11);
            CHECK(!entries[0].is_directory);
            // The CRC must match what the writer computed, which is what makes
            // skip-existing trustworthy.
            Crc32 crc;
            crc.update("hello world", 11);
            CHECK(entries[0].crc32 == crc.value());
        }
    }

    // --- CRC skip ----------------------------------------------------------
    {
        ZipArchiveExtractor extractor;

        // Unchanged on disk: skipped, so it is not in extracted_files.
        ExtractionResult again = extractor.extract(archive, destination, true);
        CHECK(again.success);
        CHECK(again.extracted_files.empty());

        // Changed on disk: the CRC differs, so it is rewritten.
        CHECK(write_file(path::combine(destination, "readme.txt"), "tampered"));
        ExtractionResult repaired = extractor.extract(archive, destination, true);
        CHECK(repaired.success);
        CHECK(repaired.extracted_files.size() == 1);
        CHECK_EQ(read_file(path::combine(destination, "readme.txt")), "hello world");
    }

    // --- progress and cancellation -----------------------------------------
    {
        ZipArchiveExtractor extractor;

        int calls = 0;
        int last_total = 0;
        ExtractionResult result = extractor.extract(
            archive, destination, false,
            [&calls, &last_total](int done, int total, long long, long long) -> bool {
                ++calls;
                last_total = total;
                (void)done;
                return true;
            });

        CHECK(result.success);
        CHECK(calls == 2);
        CHECK(last_total == 2);

        // Returning false stops the extraction and reports it as cancelled
        // rather than as a failure.
        ExtractionResult stopped = extractor.extract(
            archive, destination, false,
            [](int, int, long long, long long) -> bool { return false; });

        CHECK(!stopped.success);
        CHECK(stopped.canceled);
    }

    // --- zip slip -----------------------------------------------------------
    //
    // An archive is untrusted input, so an entry that escapes the destination
    // must fail the extraction rather than being sanitised and written.
    {
        const std::string hostile = scratch("hostile.zip");

        ZipBuilder builder;
        builder.add("safe.txt", "fine");
        builder.add("../escaped.txt", "should never be written");
        CHECK(write_file(hostile, builder.build()));

        ZipArchiveExtractor extractor;
        ExtractionResult result = extractor.extract(hostile, scratch("slip"), false);

        CHECK(!result.success);
        CHECK(result.error.find("unsafe entry path") != std::string::npos);
        CHECK(!path::exists(path::combine(path::temp_directory(),
                                          "lc_archive_test/escaped.txt")));

        path::remove_file(hostile);
    }

    // An absolute path is refused the same way.
    {
        const std::string hostile = scratch("absolute.zip");

        ZipBuilder builder;
        builder.add("/etc/passwd", "nope");
        CHECK(write_file(hostile, builder.build()));

        ZipArchiveExtractor extractor;
        ExtractionResult result = extractor.extract(hostile, scratch("abs"), false);

        CHECK(!result.success);
        CHECK(result.error.find("unsafe entry path") != std::string::npos);

        path::remove_file(hostile);
    }

    // --- a missing or corrupt archive reports rather than crashing ----------
    {
        ZipArchiveExtractor extractor;

        ExtractionResult missing =
            extractor.extract(scratch("does_not_exist.zip"), destination, false);
        CHECK(!missing.success);
        CHECK(missing.error.find("could not open archive") != std::string::npos);

        const std::string junk = scratch("junk.zip");
        CHECK(write_file(junk, "this is definitely not a zip file"));

        ExtractionResult corrupt = extractor.extract(junk, destination, false);
        CHECK(!corrupt.success);
        CHECK(extractor.list(junk).empty());

        path::remove_file(junk);
    }

    path::remove_file(archive);
}
