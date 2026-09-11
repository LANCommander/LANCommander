#ifndef LANCOMMANDER_MODELS_GAME_H
#define LANCOMMANDER_MODELS_GAME_H

#include <map>
#include <string>
#include <vector>

#include "archive.h"
#include "custom_field.h"
#include "script.h"

namespace lancommander {

enum class GameType {
    MainGame = 0,
    Expansion,
    StandaloneExpansion,
    Mod,
    StandaloneMod
};

struct Action {
    std::string name;
    std::string path;
    std::string arguments;
    std::string working_directory;
    bool is_primary = false;
    int sort_order = 0;
    std::map<std::string, std::string> variables;
};

struct MediaRef {
    std::string id;
    std::string type;
    std::string crc32;
    std::string file_id;
};

struct Game {
    std::string id;
    std::string title;
    std::string sort_title;
    std::string description;
    std::string notes;
    int released_year = 0;
    GameType type = GameType::MainGame;
    std::string base_game_id;
    bool in_library = false;
    std::string install_directory;
    std::vector<Action> actions;
    std::vector<MediaRef> media;
    std::vector<std::string> genres;
    std::vector<std::string> developers;
    std::vector<std::string> publishers;
    std::string cover_media_id;
    std::string cover_crc32;
    std::vector<Archive> archives;
};

struct ManifestAction {
    std::string name;
    std::string path;
    std::string arguments;
    std::string working_directory;
    bool is_primary = false;
    int sort_order = 0;
    std::map<std::string, std::string> variables;
};

// One file inside a save archive. `archive_path` is where it sits under
// Files/<save path id>/; `actual_path` is where it belongs on this machine,
// still in its {InstallDir}/%VAR% form so an archive packed on one machine
// restores correctly on another.
struct SavePathEntry {
    std::string archive_path;
    std::string actual_path;
};

struct ManifestSavePath {
    std::string id;
    std::string path;
    std::string working_directory;
    bool is_file = true;
    bool is_regex = false;
    // Bitmask of RuntimePlatform. A save path for a platform this is not
    // running on is skipped, exactly as scripts are.
    int platforms = RuntimePlatform_None;
    // Filled in by the packer and written into the archive's own manifest;
    // the download side reads them back to know where each file goes.
    std::vector<SavePathEntry> entries;
};

struct ManifestRedistributable {
    std::string id;
    std::string name;
};

struct GameManifest {
    std::string id;
    std::string title;
    std::string version;
    std::vector<ManifestAction> actions;
    std::vector<ManifestSavePath> save_paths;
    std::vector<ManifestRedistributable> redistributables;
    // Each custom field becomes one PowerShell variable named after the field.
    std::vector<GameCustomField> custom_fields;
    // Metadata only — script bodies live in separate .ps1 files on disk. Used
    // to gate execution on the current runtime platform.
    std::vector<Script> scripts;
};

} // namespace lancommander

#endif // LANCOMMANDER_MODELS_GAME_H
