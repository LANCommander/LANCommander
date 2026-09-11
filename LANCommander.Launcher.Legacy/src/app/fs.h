#ifndef LAUNCHER_APP_FS_H
#define LAUNCHER_APP_FS_H

#include <string>

// The small amount of filesystem the launcher actually needs.
//
// Four call sites reached straight for CreateDirectoryA, GetFileAttributesA,
// FindFirstFileA and GetTempFileNameA, which is why the media cache, the
// image cache and the download queue each pulled in <windows.h>. The set is
// small enough that a seam is cheaper than the ifdefs would have been.
//
// Paths are byte strings in the platform's own encoding, and separators may
// be either slash — every implementation here accepts both.

namespace launcher
{

    // Creates one directory. True if it now exists, including when it
    // already did; there is no "already exists" error to distinguish,
    // because no caller cares which of the two happened.
    bool fs_mkdir(const std::string &path);

    // Size of a regular file, or -1 for anything that is not one (missing,
    // a directory, unreadable). Returned as a long long so a file larger
    // than 2 GB does not read back negative on a 32-bit target.
    long long fs_file_size(const std::string &path);

    inline bool fs_exists(const std::string &path)
    {
        return fs_file_size(path) >= 0;
    }

    bool fs_remove(const std::string &path);

    // Renames `from` over `to`, replacing an existing `to`. Not atomic:
    // Windows 95 has no MOVEFILE_REPLACE_EXISTING and DOS has nothing at
    // all, so the destination is removed first. Callers rely on both names
    // holding the same bytes, which is what makes losing the race benign.
    bool fs_rename(const std::string &from, const std::string &to);

    // Removes an empty directory. False if it was not empty, which is how
    // the uninstaller leaves a directory the user put their own files in.
    bool fs_rmdir(const std::string &path);

    // A path in the system temp directory that no file currently occupies,
    // for the download queue's in-progress archive. `prefix` is at most
    // three characters on DOS, where 8.3 leaves no room for more.
    std::string fs_temp_file(const char *prefix);

} // namespace launcher

#endif // LAUNCHER_APP_FS_H
