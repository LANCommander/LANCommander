#ifndef LAUNCHER_APP_ZIP_IO_H
#define LAUNCHER_APP_ZIP_IO_H

#include <string>

#include "miniz.h"

// Zip archives backed by the launcher's own file I/O.
//
// miniz's FILE*-based entry points -- mz_zip_reader_init_cfile,
// mz_zip_writer_init_cfile, mz_zip_writer_add_cfile, mz_zip_reader_init_file
// and the rest -- reach the disk through MZ_FTELL64 / MZ_FSEEK64, which on
// its Windows branch are _ftelli64 and _fseeki64. MinGW implements those over
// SetFilePointerEx and GetFileSizeEx, and neither exists before Windows XP.
//
// That is not a runtime problem, it is a load-time one: a statically imported
// symbol the OS does not export makes the loader refuse the executable, so the
// launcher fails to start on Windows 95 and 98 with no message and never
// reaches main(). tools/deny-scan.sh is the gate that catches it.
//
// Avoiding the calls is not enough on its own, because nothing in this build
// links with --gc-sections: an unreferenced function in miniz_zip.c still
// contributes its imports. So the launcher compiles miniz with
// MINIZ_NO_STDIO -- which removes those entry points outright -- and hands it
// the callbacks below instead. They use 32-bit fseek/ftell, which caps an
// archive at 2 GB; a game archive or a save that large is not a case this
// launcher has, and the targets it runs on could not hold one.

namespace launcher
{

    // The file behind an archive. Must outlive the mz_zip_archive it was
    // opened for -- miniz keeps the pointer and calls back into it.
    struct ZipFile
    {
        FILE *file;
        long size; // reader only; the archive's length on disk

        ZipFile() : file(NULL), size(0) {}
    };

    // Opens `path` and initialises `zip` to read it. On failure nothing is
    // left open and `zip` is untouched.
    bool zip_open_read(mz_zip_archive *zip, ZipFile *io, const std::string &path);

    // Creates `path` and initialises `zip` to write it.
    bool zip_open_write(mz_zip_archive *zip, ZipFile *io, const std::string &path);

    // Closes the file. Call after mz_zip_reader_end / mz_zip_writer_end.
    void zip_close(ZipFile *io);

    // Streams `source` into the archive under `archive_name`. An empty source
    // file is stored as an empty entry rather than skipped.
    bool zip_add_file(mz_zip_archive *zip, const std::string &archive_name,
                      const std::string &source);

    // Extracts entry `index` to `path`, creating parent directories.
    bool zip_extract_to_file(mz_zip_archive *zip, unsigned int index,
                             const std::string &path);

} // namespace launcher

#endif // LAUNCHER_APP_ZIP_IO_H
