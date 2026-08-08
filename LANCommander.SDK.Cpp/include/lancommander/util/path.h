#ifndef LANCOMMANDER_UTIL_PATH_H
#define LANCOMMANDER_UTIL_PATH_H

#include <string>
#include <vector>

#include "../types.h"

namespace lancommander {
namespace path {

struct DirectoryEntry {
    std::string name;      // leaf name, not a full path
    bool is_directory;

    DirectoryEntry() : is_directory(false) {}
};

// Minimal filesystem helpers. <filesystem> is unavailable here: the SDK is
// C++14 and targets MinGW32, Open Watcom and VC6 alongside modern toolchains.
//
// Windows implementations use only ANSI Win32 APIs present on Windows 95
// (GetFileAttributesA, CreateDirectoryA, DeleteFileA, GetTempPathA,
// Get/SetCurrentDirectoryA). Everything else uses POSIX stat/mkdir/unlink.

// '\\' on Windows, '/' elsewhere.
char separator();

// Joins two components with exactly one separator. Returns the non-empty side
// when one is empty. An absolute `b` replaces `a` entirely.
std::string combine(const std::string& a, const std::string& b);

// Everything before the final separator, without a trailing one. "" when the
// path has no separator.
std::string parent(const std::string& p);

// Everything after the final separator.
std::string base_name(const std::string& p);

bool exists(const std::string& p);
bool is_directory(const std::string& p);

// Creates `p` and any missing parents. Succeeds when the directory already
// exists.
Result<bool> create_directories(const std::string& p);

// The immediate children of `p`, excluding "." and "..". Not recursive.
Result<std::vector<DirectoryEntry> > list_directory(const std::string& p);

// Succeeds when the file did not exist to begin with.
Result<bool> remove_file(const std::string& p);

// TEMP/TMP on Windows, $TMPDIR (or /tmp) elsewhere. Never has a trailing
// separator.
std::string temp_directory();

// Creates a new empty file in temp_directory() named
// "<prefix><unique><suffix>" and returns its path. The caller owns deletion.
Result<std::string> create_temp_file(const std::string& prefix,
                                     const std::string& suffix);

// "" if the current directory cannot be determined.
std::string get_current_directory();

bool set_current_directory(const std::string& p);

} // namespace path
} // namespace lancommander

#endif // LANCOMMANDER_UTIL_PATH_H
