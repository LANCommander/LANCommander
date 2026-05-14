#ifndef LANCOMMANDER_WIN9X_SAVE_SYNC_H
#define LANCOMMANDER_WIN9X_SAVE_SYNC_H

#include "game_client.h"
#include <string>
#include <vector>

// Packs files referenced by `savePaths` (File-type, non-regex only) into a
// .lcs zip at `outZipPath`. Returns false on error; on success, sets *empty to
// true when no files matched (caller may skip the upload).
bool PackSaveArchive(const std::vector<ManifestSavePath>& savePaths,
                     const std::string& installDir,
                     const std::string& outZipPath,
                     bool* empty,
                     std::string* errorOut);

// Restores files from a .lcs zip back to their original locations, using
// `savePaths` to resolve the save-path id -> working directory mapping.
bool UnpackSaveArchive(const std::string& zipPath,
                       const std::vector<ManifestSavePath>& savePaths,
                       const std::string& installDir,
                       std::string* errorOut);

#endif
