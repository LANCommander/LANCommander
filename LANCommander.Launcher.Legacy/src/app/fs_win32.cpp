// fs_win32.cpp — the fs.h seam on Windows.
//
// Deliberately still ANSI (the A entry points) and still avoiding anything
// added after Windows 95: this is the build that runs on 95 and 98.

#include "app/fs.h"

#include <windows.h>

namespace launcher
{

    bool fs_mkdir(const std::string &path)
    {
        if (path.empty())
            return false;

        if (CreateDirectoryA(path.c_str(), NULL))
            return true;

        return GetLastError() == ERROR_ALREADY_EXISTS;
    }

    long long fs_file_size(const std::string &path)
    {
        if (path.empty())
            return -1;

        // FindFirstFileA rather than GetFileAttributesExA, which Windows 95
        // does not export.
        WIN32_FIND_DATAA find;
        HANDLE h = FindFirstFileA(path.c_str(), &find);
        if (h == INVALID_HANDLE_VALUE)
            return -1;
        FindClose(h);

        if (find.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
            return -1;

        return ((long long)find.nFileSizeHigh << 32) | (long long)find.nFileSizeLow;
    }

    bool fs_remove(const std::string &path)
    {
        return !path.empty() && DeleteFileA(path.c_str()) != 0;
    }

    bool fs_rename(const std::string &from, const std::string &to)
    {
        if (from.empty() || to.empty())
            return false;

        // MoveFileExA with MOVEFILE_REPLACE_EXISTING is not on Windows 95.
        DeleteFileA(to.c_str());
        return MoveFileA(from.c_str(), to.c_str()) != 0;
    }

    bool fs_rmdir(const std::string &path)
    {
        return !path.empty() && RemoveDirectoryA(path.c_str()) != 0;
    }

    std::string fs_temp_file(const char *prefix)
    {
        char dir[MAX_PATH];
        char file[MAX_PATH];

        const DWORD n = GetTempPathA(MAX_PATH, dir);
        if (n == 0 || n >= MAX_PATH)
            return std::string();

        // Creates the file as well as naming it, which is what makes the
        // name safe to hand out.
        if (GetTempFileNameA(dir, prefix ? prefix : "lcl", 0, file) == 0)
            return std::string();

        return std::string(file);
    }

} // namespace launcher
