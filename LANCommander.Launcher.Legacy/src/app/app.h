#ifndef LAUNCHER_APP_H
#define LAUNCHER_APP_H

#include <string>
#include <vector>

#include <lancommander/lancommander.h>

#include "gfx/gfx.h"
#include "settings.h"
#include "ui/image_cache.h"
#include "app/data_store.h"
#include "app/media_prefetch.h"
#include "app/game_art_fetcher.h"
#include "app/download_queue.h"
#include "app/game_database.h"
#include "app/script_host.h"
#include "app/machine_info.h"

// Which tab is active on the library screen.
enum class LibraryTab
{
    Depot,
    Library
};

namespace launcher
{

    // Active screen in the launcher.
    enum class Screen
    {
        // Step one of signing in: pick or discover a server. Only reached on
        // first run, or when the user asks to change server from Login.
        ServerSelect,
        Login,
        Library,
        Depot,
        DepotBrowse,
        GameDetail,
        Downloads,
        Settings,
        // The script console: what a game's Install.ps1 printed, and where a
        // breakpoint is set. Reached from Settings, and from a game's detail
        // page once it has run something.
        ScriptConsole
    };

    // What the depot browse grid is currently narrowed to.
    enum class DepotFilterKind
    {
        None,       // the whole catalogue
        Genre,
        Collection,
        Search
    };

    // Application state and lifecycle.
    class App
    {
    public:
        App();
        ~App();

        // Initialize the display, create the window, load settings.
        bool init(int width, int height);

        // Run the main loop. Returns the exit code.
        int run();

        // Tear down the display and save settings.
        void shutdown();

        // --- State accessors (used by screens) ---

        lancommander::IHttpClient &http();
        lancommander::AuthenticationClient &auth();
        lancommander::ConnectionClient &connection();
        lancommander::GameClient &games();
        lancommander::LibraryClient &library();
        lancommander::MediaClient &media();
        lancommander::ToolClient &tools();
        lancommander::DepotClient &depot();
        lancommander::LauncherClient &launcher_client();
        lancommander::KeyClient &keys();
        lancommander::SaveClient &saves();

        Settings &settings();

        gfx::Surface *backbuffer();
        int screen_width() const;
        int screen_height() const;

        void switch_screen(Screen screen);
        Screen current_screen() const;

        void go_back();

        void set_overlay_active(bool active);
        bool overlay_active() const;

        // Set the game ID to show on the detail screen.
        void set_selected_game(const std::string &game_id);
        std::string selected_game() const;

        // Currently authenticated user alias.
        void set_user_alias(const std::string &alias);
        const std::string &user_alias() const;

        const std::string &user_id() const;

        void refresh_profile();

        bool has_avatar() const;

        const std::string &avatar_path() const;


        DepotData &depot_data();
        LibraryData &library_data();

        void ensure_depot_loaded();
        void ensure_library_loaded();

        void ensure_play_sessions_loaded();

        void recent_game_ids(int limit, std::vector<std::string> *out);

        // UTC epoch of the last finished session for `game_id`, or 0.
        long long last_played_at(const std::string &game_id);

        // Total seconds played across all finished sessions, or 0.
        long long total_play_seconds(const std::string &game_id);

        void ensure_game_art_loaded();

        GameArt game_art(const std::string &game_id) const;

        void request_game_art(const std::string &game_id);

        void invalidate_depot();
        void invalidate_library();

        LibraryTab library_tab() const;
        void set_library_tab(LibraryTab tab);

        void set_depot_filter(DepotFilterKind kind, const std::string &value);
        DepotFilterKind depot_filter_kind() const;
        const std::string &depot_filter_value() const;

        // Image cache for game art.
        ui::ImageCache &image_cache();

        MediaPrefetch &media_prefetch();

        // Download queue.
        DownloadQueue &downloads();

        // Local database of installed games.
        GameDatabase &game_db();

        // Lifecycle scripts, their captured output, and the debugger.
        ScriptHost &script_host();

        // Turns script debugging on or off, applies it to the host and writes
        // Settings.yml. One entry point rather than several, because the
        // setting is the single source of truth: the console's Dbg button and
        // the Settings screen's checkbox are two views of this one value.
        //
        // Enabling it also arms break-on-entry, so the next script that runs
        // stops on its first statement. That is the only way to debug a script
        // the launcher starts on its own -- an install begins and ends before
        // there is any moment to press anything -- and it is what makes the
        // switch do something visible rather than only mattering once a
        // breakpoint has been set.
        void set_script_debugging(bool enabled);
        bool script_debugging() const;

        // Draws one frame while a script is paused on THIS thread, and returns
        // false when the app is quitting. Installed as the ScriptHost debug
        // pump: a script stopped at a breakpoint on the UI thread has halted
        // the only thing that draws, so it has to drive a frame itself or
        // there would be no Continue button to press. See ScriptHost.
        bool pump_debug_frame();

        // Request the app to quit.
        void quit();
        bool should_quit() const;

        // Resize support — called from WndProc, applied in the main loop.
        void request_resize(int new_w, int new_h);
        void apply_pending_resize();

    private:
        // The display, backbuffer and their dimensions are owned by gfx.

        // SDK (IHttpClient* — concrete type created in app.cpp)
        lancommander::IHttpClient *m_http;
        lancommander::AuthenticationClient *m_auth;
        lancommander::ConnectionClient *m_connection;
        lancommander::GameClient *m_games;
        lancommander::LibraryClient *m_library;
        lancommander::MediaClient *m_media;
        lancommander::ToolClient *m_tools;
        lancommander::DepotClient *m_depot;
        lancommander::LauncherClient *m_launcher;
        lancommander::PlaySessionClient *m_play_sessions_client;
        lancommander::KeyClient *m_keys;
        lancommander::SaveClient *m_saves;

        // Identity for key allocation. Owned here because KeyClient holds a
        // reference to it for its whole life.
        PlatformMachineInfo m_machine;
        ui::ImageCache *m_image_cache;

        lancommander::IHttpClient *m_prefetch_http;
        lancommander::MediaClient *m_prefetch_media;
        MediaPrefetch *m_prefetch;

        lancommander::IHttpClient *m_art_http;
        lancommander::GameClient *m_art_games;
        GameArtFetcher *m_art_fetcher;

        // The script host fetches a game's scripts and manifest from the
        // install worker, so it gets its own client for the same reason the
        // prefetcher and the art fetcher do.
        lancommander::IHttpClient *m_script_http;
        lancommander::GameClient *m_script_games;
        lancommander::ScriptClient *m_script_client;
        ScriptHost *m_script_host;

        // App state
        Settings m_settings;
        Screen m_current_screen;
        std::string m_selected_game;
        std::string m_user_alias;
        std::string m_user_id;
        std::string m_avatar_path;
        bool m_has_avatar;
        DepotData m_depot_data;
        LibraryData m_library_data;
        GameArtIndex m_game_art;
        PlaySessionIndex m_play_sessions;
        std::vector<Screen> m_nav_stack;
        DownloadQueue m_downloads;
        GameDatabase m_game_db;
        LibraryTab m_library_tab;
        DepotFilterKind m_depot_filter_kind;
        std::string m_depot_filter_value;
        bool m_overlay_active;
        bool m_quit;

        // Pending resize. Written from the WndProc -- which Allegro runs on
        // its own window thread, not this one -- and consumed in the main
        // loop, so volatile: without it the compiler is entitled to keep
        // m_resize_pending in a register across the loop body and never see
        // the flag go true.
        volatile bool m_resize_pending;
        volatile int m_pending_width;
        volatile int m_pending_height;
    };

} // namespace launcher

#endif // LAUNCHER_APP_H
