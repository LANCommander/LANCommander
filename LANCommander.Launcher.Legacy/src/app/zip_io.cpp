#include "app/zip_io.h"

#include <lancommander/util/path.h>

#include <cstdio>
#include <cstring>

namespace launcher
{

    namespace
    {
        // miniz asks for bytes at an absolute offset and does not assume the
        // stream is where it left it, so every call seeks. mz_uint64 is
        // narrowed to long here, which is the 2 GB cap described in the header.
        std::size_t read_at(void *opaque, mz_uint64 file_ofs, void *buffer,
                            std::size_t n)
        {
            ZipFile *io = (ZipFile *)opaque;

            if (!io || !io->file)
                return 0;

            // Refuse rather than truncate: a silent wrap here would read the
            // wrong part of the archive and produce plausible garbage.
            if (file_ofs > (mz_uint64)0x7FFFFFFF)
                return 0;

            if (fseek(io->file, (long)file_ofs, SEEK_SET) != 0)
                return 0;

            return fread(buffer, 1, n, io->file);
        }

        std::size_t write_at(void *opaque, mz_uint64 file_ofs, const void *buffer,
                             std::size_t n)
        {
            ZipFile *io = (ZipFile *)opaque;

            if (!io || !io->file)
                return 0;

            if (file_ofs > (mz_uint64)0x7FFFFFFF)
                return 0;

            if (fseek(io->file, (long)file_ofs, SEEK_SET) != 0)
                return 0;

            return fwrite(buffer, 1, n, io->file);
        }

        // Feeds one file into the writer. Same shape as read_at, but over the
        // source file rather than the archive.
        std::size_t read_source(void *opaque, mz_uint64 file_ofs, void *buffer,
                                std::size_t n)
        {
            FILE *source = (FILE *)opaque;

            if (!source)
                return 0;

            if (file_ofs > (mz_uint64)0x7FFFFFFF)
                return 0;

            if (fseek(source, (long)file_ofs, SEEK_SET) != 0)
                return 0;

            return fread(buffer, 1, n, source);
        }

        // Receives one extracted entry.
        std::size_t write_extracted(void *opaque, mz_uint64 file_ofs,
                                    const void *buffer, std::size_t n)
        {
            (void)file_ofs; // extraction is sequential
            FILE *out = (FILE *)opaque;

            if (!out)
                return 0;

            return fwrite(buffer, 1, n, out);
        }

        std::string parent_of(const std::string &path)
        {
            const std::string::size_type cut = path.find_last_of("/\\");
            return cut == std::string::npos ? std::string() : path.substr(0, cut);
        }
    } // namespace

    bool zip_open_read(mz_zip_archive *zip, ZipFile *io, const std::string &path)
    {
        if (!zip || !io)
            return false;

        io->file = fopen(path.c_str(), "rb");
        if (!io->file)
            return false;

        if (fseek(io->file, 0, SEEK_END) != 0)
        {
            zip_close(io);
            return false;
        }

        io->size = ftell(io->file);

        if (io->size <= 0 || fseek(io->file, 0, SEEK_SET) != 0)
        {
            zip_close(io);
            return false;
        }

        memset(zip, 0, sizeof(*zip));
        zip->m_pRead = &read_at;
        zip->m_pIO_opaque = io;

        if (!mz_zip_reader_init(zip, (mz_uint64)io->size, 0))
        {
            zip_close(io);
            return false;
        }

        return true;
    }

    bool zip_open_write(mz_zip_archive *zip, ZipFile *io, const std::string &path)
    {
        if (!zip || !io)
            return false;

        io->file = fopen(path.c_str(), "wb");
        if (!io->file)
            return false;

        io->size = 0;

        memset(zip, 0, sizeof(*zip));
        zip->m_pWrite = &write_at;
        zip->m_pIO_opaque = io;

        // 0: a new archive rather than an append.
        if (!mz_zip_writer_init(zip, 0))
        {
            zip_close(io);
            return false;
        }

        return true;
    }

    void zip_close(ZipFile *io)
    {
        if (io && io->file)
        {
            fclose(io->file);
            io->file = NULL;
        }
    }

    bool zip_add_file(mz_zip_archive *zip, const std::string &archive_name,
                      const std::string &source)
    {
        if (!zip)
            return false;

        FILE *in = fopen(source.c_str(), "rb");
        if (!in)
            return false;

        if (fseek(in, 0, SEEK_END) != 0)
        {
            fclose(in);
            return false;
        }

        const long size = ftell(in);

        if (size < 0 || fseek(in, 0, SEEK_SET) != 0)
        {
            fclose(in);
            return false;
        }

        if (size == 0)
        {
            // A genuinely empty save file still belongs in the archive, and
            // the streaming path cannot express a zero-length entry: miniz
            // gates its whole copy loop on `if (max_size)`.
            fclose(in);
            return mz_zip_writer_add_mem_ex(zip, archive_name.c_str(), "", 0,
                                            NULL, 0, MZ_DEFAULT_COMPRESSION,
                                            0, 0) != 0;
        }

        // max_size is the length to write here, not a cap.
        const mz_bool ok = mz_zip_writer_add_read_buf_callback(
            zip, archive_name.c_str(), &read_source, in, (mz_uint64)size,
            NULL, NULL, 0, MZ_DEFAULT_COMPRESSION, NULL, 0, NULL, 0);

        fclose(in);

        return ok != 0;
    }

    bool zip_extract_to_file(mz_zip_archive *zip, unsigned int index,
                             const std::string &path)
    {
        if (!zip)
            return false;

        const std::string parent = parent_of(path);
        if (!parent.empty())
            lancommander::path::create_directories(parent);

        FILE *out = fopen(path.c_str(), "wb");
        if (!out)
            return false;

        const mz_bool ok = mz_zip_reader_extract_to_callback(
            zip, index, &write_extracted, out, 0);

        fclose(out);

        return ok != 0;
    }

} // namespace launcher
