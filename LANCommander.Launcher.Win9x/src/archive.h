#ifndef LANCOMMANDER_WIN9X_ARCHIVE_H
#define LANCOMMANDER_WIN9X_ARCHIVE_H

#include <string>

typedef bool (*ExtractProgressFn)(unsigned long fileIndex,
                                  unsigned long fileCount,
                                  const char* currentName,
                                  void* userData);

bool ExtractZip(const std::string& zipPath, const std::string& destDir,
                ExtractProgressFn progress, void* userData,
                std::string* errorOut);

class ZipWriter
{
public:
    ZipWriter();
    ~ZipWriter();

    bool Open(const std::string& path, std::string* errorOut);
    bool AddFile(const std::string& archivePath, const std::string& srcFile,
                 std::string* errorOut);
    bool Close(std::string* errorOut);

    bool IsEmpty() const { return m_count == 0; }

private:
    void* m_zip; // mz_zip_archive*
    bool  m_open;
    int   m_count;
};

class ZipReader
{
public:
    ZipReader();
    ~ZipReader();

    bool Open(const std::string& path, std::string* errorOut);
    void Close();

    unsigned int Count() const;
    bool EntryName(unsigned int i, std::string* name) const;
    bool IsDir(unsigned int i) const;
    bool ExtractTo(unsigned int i, const std::string& destFile,
                   std::string* errorOut);

private:
    void* m_zip; // mz_zip_archive*
    bool  m_open;
};

#endif
