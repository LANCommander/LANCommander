#include "lancommander/util/path.h"

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#else
#include <dirent.h>
#include <sys/stat.h>
#include <sys/types.h>
#include <unistd.h>
#include <cstdlib>
#endif

#include <cstdio>
#include <vector>

namespace lancommander {
namespace path {

namespace {

bool is_separator(char c)
{
#ifdef _WIN32
    // Windows accepts both when *reading* a path, even though we always emit
    // backslashes.
    return c == '\\' || c == '/';
#else
    return c == '/';
#endif
}

// "C:\foo", "\\server\share" or "/foo" — a path that must not be appended to
// anything.
bool is_absolute(const std::string& p)
{
    if (p.empty())
        return false;
    if (is_separator(p[0]))
        return true;
#ifdef _WIN32
    if (p.size() >= 2 && p[1] == ':')
        return true;
#endif
    return false;
}

std::string strip_trailing_separators(const std::string& p)
{
    std::string out = p;
    // Keep a lone "/" and a bare drive root ("C:\") intact.
    while (out.size() > 1 && is_separator(out[out.size() - 1])) {
#ifdef _WIN32
        if (out.size() == 3 && out[1] == ':')
            break;
#endif
        out.erase(out.size() - 1);
    }
    return out;
}

} // namespace

char separator()
{
#ifdef _WIN32
    return '\\';
#else
    return '/';
#endif
}

std::string combine(const std::string& a, const std::string& b)
{
    if (a.empty())
        return b;
    if (b.empty())
        return a;
    if (is_absolute(b))
        return b;

    std::string out = a;
    if (!is_separator(out[out.size() - 1]))
        out += separator();

    std::string tail = b;
    while (!tail.empty() && is_separator(tail[0]))
        tail.erase(0, 1);

    return out + tail;
}

std::string parent(const std::string& p)
{
    const std::string trimmed = strip_trailing_separators(p);
    for (std::size_t i = trimmed.size(); i > 0; --i) {
        if (is_separator(trimmed[i - 1]))
            return strip_trailing_separators(trimmed.substr(0, i - 1));
    }
    return std::string();
}

std::string base_name(const std::string& p)
{
    const std::string trimmed = strip_trailing_separators(p);
    for (std::size_t i = trimmed.size(); i > 0; --i) {
        if (is_separator(trimmed[i - 1]))
            return trimmed.substr(i);
    }
    return trimmed;
}

bool exists(const std::string& p)
{
    if (p.empty())
        return false;
#ifdef _WIN32
    return GetFileAttributesA(p.c_str()) != INVALID_FILE_ATTRIBUTES;
#else
    struct stat st;
    return ::stat(p.c_str(), &st) == 0;
#endif
}

bool is_directory(const std::string& p)
{
    if (p.empty())
        return false;
#ifdef _WIN32
    const DWORD attrs = GetFileAttributesA(p.c_str());
    return attrs != INVALID_FILE_ATTRIBUTES &&
           (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0;
#else
    struct stat st;
    if (::stat(p.c_str(), &st) != 0)
        return false;
    return S_ISDIR(st.st_mode);
#endif
}

Result<bool> create_directories(const std::string& p)
{
    if (p.empty())
        return Result<bool>::fail("cannot create a directory with an empty path");

    const std::string target = strip_trailing_separators(p);

    if (is_directory(target))
        return Result<bool>::ok(true);
    if (exists(target))
        return Result<bool>::fail("path exists but is not a directory: " + target);

    const std::string up = parent(target);
    if (!up.empty() && !is_directory(up)) {
        Result<bool> parent_result = create_directories(up);
        if (!parent_result)
            return parent_result;
    }

#ifdef _WIN32
    if (!CreateDirectoryA(target.c_str(), NULL)) {
        // Lost a race with another creator — that is still success.
        if (GetLastError() != ERROR_ALREADY_EXISTS)
            return Result<bool>::fail("could not create directory: " + target);
    }
#else
    if (::mkdir(target.c_str(), 0755) != 0 && !is_directory(target))
        return Result<bool>::fail("could not create directory: " + target);
#endif

    return Result<bool>::ok(true);
}

Result<std::vector<DirectoryEntry> > list_directory(const std::string& p)
{
    typedef std::vector<DirectoryEntry> Entries;

    if (p.empty())
        return Result<Entries>::fail("cannot list a directory with an empty path");

    Entries entries;

#ifdef _WIN32
    // FindFirstFileA / FindNextFileA are ANSI and present on Windows 95.
    const std::string pattern = combine(p, "*");

    WIN32_FIND_DATAA data;
    HANDLE handle = FindFirstFileA(pattern.c_str(), &data);

    if (handle == INVALID_HANDLE_VALUE) {
        if (GetLastError() == ERROR_FILE_NOT_FOUND)
            return Result<Entries>::ok(entries);   // an empty directory
        return Result<Entries>::fail("could not list directory: " + p);
    }

    do {
        const std::string name = data.cFileName;
        if (name == "." || name == "..")
            continue;

        DirectoryEntry entry;
        entry.name = name;
        entry.is_directory =
            (data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0;
        entries.push_back(entry);
    } while (FindNextFileA(handle, &data));

    FindClose(handle);
#else
    DIR* dir = ::opendir(p.c_str());
    if (!dir)
        return Result<Entries>::fail("could not list directory: " + p);

    for (;;) {
        struct dirent* item = ::readdir(dir);
        if (!item)
            break;

        const std::string name = item->d_name;
        if (name == "." || name == "..")
            continue;

        DirectoryEntry entry;
        entry.name = name;
        // stat rather than d_type: the latter is not portable, and some
        // filesystems report DT_UNKNOWN.
        entry.is_directory = is_directory(combine(p, name));
        entries.push_back(entry);
    }

    ::closedir(dir);
#endif

    return Result<Entries>::ok(entries);
}

Result<bool> remove_file(const std::string& p)
{
    if (p.empty())
        return Result<bool>::fail("cannot delete a file with an empty path");
    if (!exists(p))
        return Result<bool>::ok(true);

#ifdef _WIN32
    if (!DeleteFileA(p.c_str()))
        return Result<bool>::fail("could not delete file: " + p);
#else
    if (::unlink(p.c_str()) != 0)
        return Result<bool>::fail("could not delete file: " + p);
#endif

    return Result<bool>::ok(true);
}

std::string temp_directory()
{
#ifdef _WIN32
    char buffer[MAX_PATH + 1];
    const DWORD len = GetTempPathA(MAX_PATH, buffer);
    if (len == 0 || len > MAX_PATH)
        return ".";
    return strip_trailing_separators(std::string(buffer, len));
#else
    const char* env = std::getenv("TMPDIR");
    if (env != NULL && env[0] != '\0')
        return strip_trailing_separators(env);
    return "/tmp";
#endif
}

Result<std::string> create_temp_file(const std::string& prefix,
                                     const std::string& suffix)
{
    const std::string dir = temp_directory();
    static unsigned long counter = 0;

    // GetTempFileNameA is Windows-only and mkstemp is POSIX-only, and neither
    // can give the file a chosen suffix in one step. One portable loop instead.
    unsigned long seed;
#ifdef _WIN32
    seed = (unsigned long)GetCurrentProcessId() ^ (unsigned long)GetTickCount();
#else
    seed = (unsigned long)::getpid() ^ (unsigned long)std::time(NULL);
#endif

    for (int attempt = 0; attempt < 64; ++attempt) {
        char unique[24];
        std::sprintf(unique, "%08lx%04lx", seed, (counter++ & 0xFFFFUL));

        const std::string full = combine(dir, prefix + unique + suffix);

        // Benign TOCTOU: these are process-local and short-lived, so the worst
        // case is overwriting another attempt's file.
        if (exists(full))
            continue;

        std::FILE* file = std::fopen(full.c_str(), "wb");
        if (!file)
            continue;
        std::fclose(file);

        return Result<std::string>::ok(full);
    }

    return Result<std::string>::fail("could not create a temporary file in " + dir);
}

std::string get_current_directory()
{
#ifdef _WIN32
    // Ask for the required size first — a CWD may exceed MAX_PATH.
    const DWORD needed = GetCurrentDirectoryA(0, NULL);
    if (needed == 0)
        return std::string();

    std::vector<char> buffer(needed + 1, '\0');
    const DWORD written = GetCurrentDirectoryA(needed, &buffer[0]);
    if (written == 0 || written >= needed + 1)
        return std::string();

    return std::string(&buffer[0], written);
#else
    std::vector<char> buffer(1024);
    for (;;) {
        if (::getcwd(&buffer[0], buffer.size()) != NULL)
            return std::string(&buffer[0]);
        if (buffer.size() >= 65536)
            return std::string();
        buffer.resize(buffer.size() * 2);
    }
#endif
}

bool set_current_directory(const std::string& p)
{
    if (p.empty())
        return false;
#ifdef _WIN32
    return SetCurrentDirectoryA(p.c_str()) != 0;
#else
    return ::chdir(p.c_str()) == 0;
#endif
}

} // namespace path
} // namespace lancommander
