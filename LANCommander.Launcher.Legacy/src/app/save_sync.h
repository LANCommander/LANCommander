#ifndef LAUNCHER_APP_SAVE_SYNC_H
#define LAUNCHER_APP_SAVE_SYNC_H

#include <string>
#include <vector>

#include <lancommander/models/game.h>

// Cloud saves: pack what a game wrote, send it up, and put a downloaded
// archive back where the files belong.
//
// The archive is a zip laid out the way LANCommander.SDK's SavePacker writes
// it, because either launcher must be able to restore the other's save:
//
//   Manifest.yml                     the game manifest, save paths included
//   Files/<save path id>/<relative>  one tree per save path
//
// A path inside the manifest is stored in its token form -- {InstallDir}/x,
// %APPDATA%/y -- so an archive packed on one machine restores on another where
// the game is installed somewhere else entirely. See game_metadata.h.

namespace lancommander
{
    class SaveClient;
}

namespace launcher
{

    class ScriptHost;

    // Everything the sync needs. Kept free of App so it can be exercised
    // without a window.
    struct SaveSyncContext
    {
        lancommander::SaveClient *saves;
        ScriptHost *scripts; // may be null; only used for the Save* scripts

        std::string game_id;
        std::string title;
        std::string install_dir;
        std::string server_address;
        std::string default_install_dir;

        // False on the download worker. Decides how a breakpoint in a Save
        // script waits -- see ScriptTarget::on_ui_thread.
        bool on_ui_thread;

        SaveSyncContext() : saves(NULL), scripts(NULL), on_ui_thread(true) {}
    };

    // What a sync did, for the caller to report. `entries` is how many files
    // moved in either direction, which is the number that tells a user whether
    // anything actually happened.
    struct SaveSyncResult
    {
        bool ok;
        bool had_anything; // false when the game has no save paths, or no save
                           // exists on the server yet
        int entries;
        std::string error;

        SaveSyncResult() : ok(false), had_anything(false), entries(0) {}
    };

    // Fetches the latest save for the game and restores its files.
    //
    // A game with no save on the server is a success with had_anything false:
    // the first time anyone plays it there is nothing to download, and that is
    // not a failure anyone should be told about.
    SaveSyncResult save_download(const SaveSyncContext &ctx);

    // Packs the game's save paths and uploads them.
    //
    // Nothing is uploaded when the game wrote no save files, so quitting a
    // game that never saved does not replace a good cloud save with an empty
    // one.
    SaveSyncResult save_upload(const SaveSyncContext &ctx);

    // --- exposed for testing -------------------------------------------------

    // Resolves a save path to the files it currently covers on this machine.
    // A non-regex path is one entry whether or not it exists; a regex path is
    // every file under its working directory whose relative path matches.
    std::vector<lancommander::SavePathEntry> save_path_entries(
        const lancommander::ManifestSavePath &save_path,
        const std::string &install_dir);

} // namespace launcher

#endif // LAUNCHER_APP_SAVE_SYNC_H
