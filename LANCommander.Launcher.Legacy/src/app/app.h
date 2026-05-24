#ifndef LAUNCHER_APP_H
#define LAUNCHER_APP_H

#include <string>
#include <vector>

#include <lancommander/lancommander.h>

#include "settings.h"
#include "ui/image_cache.h"
#include "app/download_queue.h"
#include "app/game_database.h"

// Which tab is active on the library screen.
enum class LibraryTab
{
    Depot,
    Library
};

// Forward declarations for Allegro types — avoids pulling <allegro.h> into headers.
struct BITMAP;

namespace launcher
{

    // Active screen in the launcher.
    enum class Screen
    {
        Login,
        Library,
        GameDetail,
        Downloads,
        Settings
    };

    // Application state and lifecycle.
    class App
    {
    public:
        App();
        ~App();

        // Initialize Allegro, create the window, load settings.
        bool init(int width, int height);

        // Run the main loop. Returns the exit code.
        int run();

        // Shut down Allegro.
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

        Settings &settings();

        BITMAP *backbuffer();
        int screen_width() const;
        int screen_height() const;

        void switch_screen(Screen screen);
        Screen current_screen() const;

        // Set the game ID to show on the detail screen.
        void set_selected_game(const std::string &game_id);
        std::string selected_game() const;

        // Currently authenticated user alias.
        void set_user_alias(const std::string &alias);
        std::string user_alias() const;

        // Cached game list (refreshed on library screen entry).
        std::vector<lancommander::Game> &game_cache();

        // Cached depot game list.
        std::vector<lancommander::DepotGame> &depot_cache();

        // Active library tab.
        LibraryTab library_tab() const;
        void set_library_tab(LibraryTab tab);

        // Image cache for game art.
        ui::ImageCache &image_cache();

        // Download queue.
        DownloadQueue &downloads();

        // Local database of installed games.
        GameDatabase &game_db();

        // Request the app to quit.
        void quit();
        bool should_quit() const;

        // Resize support — called from WndProc, applied in the main loop.
        void request_resize(int new_w, int new_h);
        void apply_pending_resize();

    private:
        // Allegro
        int m_width;
        int m_height;
        BITMAP *m_backbuffer;

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
        ui::ImageCache *m_image_cache;

        // App state
        Settings m_settings;
        Screen m_current_screen;
        std::string m_selected_game;
        std::string m_user_alias;
        std::vector<lancommander::Game> m_game_cache;
        std::vector<lancommander::DepotGame> m_depot_cache;
        DownloadQueue m_downloads;
        GameDatabase m_game_db;
        LibraryTab m_library_tab;
        bool m_quit;

        // Pending resize (set from WndProc, consumed in main loop)
        bool m_resize_pending;
        int m_pending_width;
        int m_pending_height;
    };

} // namespace launcher

#endif // LAUNCHER_APP_H
