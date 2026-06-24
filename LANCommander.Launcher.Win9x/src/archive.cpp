#include "archive.h"

#include "miniz.h"

#include <windows.h>
#include <direct.h>

#include <cstdio>
#include <string>

namespace
{
    void EnsureDirectory(const std::string& path)
    {
        if (path.empty()) return;
        DWORD attrs = GetFileAttributesA(path.c_str());
        if (attrs != INVALID_FILE_ATTRIBUTES &&
            (attrs & FILE_ATTRIBUTE_DIRECTORY))
            return;
        _mkdir(path.c_str());
    }

    void EnsureParents(const std::string& fullPath)
    {
        std::string cur;
        cur.reserve(fullPath.size());
        for (size_t i = 0; i < fullPath.size(); ++i)
        {
            char c = fullPath[i];
            if (c == '/' || c == '\\')
            {
                if (!cur.empty() && cur[cur.size() - 1] != ':')
                    EnsureDirectory(cur);
                cur += '\\';
            }
            else
            {
                cur += c;
            }
        }
    }

    std::string JoinPath(const std::string& dir, const std::string& name)
    {
        std::string out = dir;
        if (!out.empty() && out[out.size() - 1] != '\\' &&
            out[out.size() - 1] != '/')
            out += '\\';
        for (size_t i = 0; i < name.size(); ++i)
            out += (name[i] == '/') ? '\\' : name[i];
        return out;
    }
}

bool ExtractZip(const std::string& zipPath, const std::string& destDir,
                ExtractProgressFn progress, void* userData,
                std::string* errorOut)
{
    EnsureDirectory(destDir);

    mz_zip_archive zip;
    memset(&zip, 0, sizeof(zip));
    if (!mz_zip_reader_init_file(&zip, zipPath.c_str(), 0))
    {
        if (errorOut) *errorOut = "Could not open archive";
        return false;
    }

    unsigned int count = mz_zip_reader_get_num_files(&zip);
    for (unsigned int i = 0; i < count; ++i)
    {
        mz_zip_archive_file_stat st;
        if (!mz_zip_reader_file_stat(&zip, i, &st))
            continue;

        std::string out = JoinPath(destDir, st.m_filename);

        if (mz_zip_reader_is_file_a_directory(&zip, i))
        {
            EnsureDirectory(out);
        }
        else
        {
            EnsureParents(out);
            if (!mz_zip_reader_extract_to_file(&zip, i, out.c_str(), 0))
            {
                mz_zip_reader_end(&zip);
                if (errorOut) *errorOut = std::string("Failed extracting ") + st.m_filename;
                return false;
            }
        }

        if (progress && !progress(i + 1, count, st.m_filename, userData))
        {
            mz_zip_reader_end(&zip);
            if (errorOut) *errorOut = "Extraction aborted";
            return false;
        }
    }

    mz_zip_reader_end(&zip);
    return true;
}

// --- ZipWriter ---

ZipWriter::ZipWriter() : m_zip(NULL), m_open(false), m_count(0) {}
ZipWriter::~ZipWriter() { std::string e; Close(&e); }

bool ZipWriter::Open(const std::string& path, std::string* errorOut)
{
    if (m_open) return true;
    mz_zip_archive* zip = (mz_zip_archive*)calloc(1, sizeof(mz_zip_archive));
    if (!zip) { if (errorOut) *errorOut = "Out of memory"; return false; }
    if (!mz_zip_writer_init_file(zip, path.c_str(), 0))
    {
        free(zip);
        if (errorOut) *errorOut = "Could not create archive";
        return false;
    }
    m_zip = zip;
    m_open = true;
    m_count = 0;
    return true;
}

bool ZipWriter::AddFile(const std::string& archivePath, const std::string& srcFile,
                        std::string* errorOut)
{
    if (!m_open) { if (errorOut) *errorOut = "Archive not open"; return false; }
    if (!mz_zip_writer_add_file((mz_zip_archive*)m_zip,
                                archivePath.c_str(), srcFile.c_str(),
                                NULL, 0, MZ_NO_COMPRESSION))
    {
        if (errorOut) *errorOut = std::string("Failed adding ") + srcFile;
        return false;
    }
    ++m_count;
    return true;
}

bool ZipWriter::Close(std::string* errorOut)
{
    if (!m_open) return true;
    mz_zip_archive* zip = (mz_zip_archive*)m_zip;
    bool ok = mz_zip_writer_finalize_archive(zip) &&
              mz_zip_writer_end(zip);
    free(zip);
    m_zip = NULL;
    m_open = false;
    if (!ok && errorOut) *errorOut = "Failed finalizing archive";
    return ok;
}

// --- ZipReader ---

ZipReader::ZipReader() : m_zip(NULL), m_open(false) {}
ZipReader::~ZipReader() { Close(); }

bool ZipReader::Open(const std::string& path, std::string* errorOut)
{
    if (m_open) return true;
    mz_zip_archive* zip = (mz_zip_archive*)calloc(1, sizeof(mz_zip_archive));
    if (!zip) { if (errorOut) *errorOut = "Out of memory"; return false; }
    if (!mz_zip_reader_init_file(zip, path.c_str(), 0))
    {
        free(zip);
        if (errorOut) *errorOut = "Could not open archive";
        return false;
    }
    m_zip = zip;
    m_open = true;
    return true;
}

void ZipReader::Close()
{
    if (!m_open) return;
    mz_zip_reader_end((mz_zip_archive*)m_zip);
    free(m_zip);
    m_zip = NULL;
    m_open = false;
}

unsigned int ZipReader::Count() const
{
    if (!m_open) return 0;
    return mz_zip_reader_get_num_files((mz_zip_archive*)m_zip);
}

bool ZipReader::EntryName(unsigned int i, std::string* name) const
{
    if (!m_open) return false;
    mz_zip_archive_file_stat st;
    if (!mz_zip_reader_file_stat((mz_zip_archive*)m_zip, i, &st))
        return false;
    *name = st.m_filename;
    return true;
}

bool ZipReader::IsDir(unsigned int i) const
{
    if (!m_open) return false;
    return mz_zip_reader_is_file_a_directory((mz_zip_archive*)m_zip, i) != 0;
}

bool ZipReader::ExtractTo(unsigned int i, const std::string& destFile,
                          std::string* errorOut)
{
    if (!m_open) { if (errorOut) *errorOut = "Archive not open"; return false; }
    EnsureParents(destFile);
    if (!mz_zip_reader_extract_to_file((mz_zip_archive*)m_zip, i, destFile.c_str(), 0))
    {
        if (errorOut) *errorOut = "Failed extracting entry";
        return false;
    }
    return true;
}
