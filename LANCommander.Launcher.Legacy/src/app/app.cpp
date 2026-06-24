// Allegro 4 must be included before Windows headers to avoid BITMAP conflict.
#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "app/app.h"
#include "app/logger.h"
#include "ui/input.h"
#include "ui/theme.h"
#include "ui/image_decoder.h"
#include "ui/window_chrome.h"
#include "ui/screen_login.h"
#include "ui/screen_library.h"
#include "ui/screen_game_detail.h"
#include "ui/screen_downloads.h"
#include "ui/screen_settings.h"

// Pull in the WinINet backend header (resolved via include paths from CMake).
#include "wininet_http_client.h"

namespace launcher
{

    static const char *DATA_DIR      = "Data";
    static const char *SETTINGS_FILE = "Data\\Settings.yml";
    static const char *GAME_DB_FILE  = "Data\\LANCommander.db";
    static const char *LOG_DIR       = "Data\\Logs";
    static const char *MEDIA_DIR     = "Data\\Media";

    // Allegro close-button callback (Alt+F4, taskbar Close, etc.).
    static volatile int s_close_requested = 0;
    static void close_button_handler() { s_close_requested = 1; }
    END_OF_STATIC_FUNCTION(close_button_handler)

    App::App()
        : m_width(0), m_height(0), m_backbuffer(NULL), m_http(NULL), m_auth(NULL), m_connection(NULL), m_games(NULL), m_library(NULL), m_media(NULL), m_tools(NULL), m_depot(NULL), m_launcher(NULL), m_image_cache(NULL), m_current_screen(Screen::Login), m_library_tab(LibraryTab::Depot), m_quit(false), m_resize_pending(false), m_pending_width(0), m_pending_height(0)
    {
    }

    App::~App()
    {
        delete m_image_cache;
        delete m_launcher;
        delete m_depot;
        delete m_tools;
        delete m_media;
        delete m_library;
        delete m_games;
        delete m_connection;
        delete m_auth;
        delete m_http;
    }

    bool App::init(int width, int height)
    {
        m_width = width;
        m_height = height;

        // Create the Data directory structure.
        CreateDirectoryA(DATA_DIR, NULL);

        log_init(LOG_DIR);

        // --- Allegro initialization ---
        if (allegro_init() != 0)
        {
            log_error("allegro_init() failed");
            return false;
        }

        install_keyboard();
        install_mouse();
        install_timer();

        set_color_depth(32);

        if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, m_width, m_height, 0, 0) != 0)
        {
            // Fall back to 16-bit if 32-bit isn't available (Win9x compatibility)
            set_color_depth(16);

            if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, m_width, m_height, 0, 0) != 0)
                return false;
        }

        set_window_title("LANCommander");

        // Don't use show_mouse(screen) — it fights with backbuffer
        // blitting. We draw our own cursor on the backbuffer instead.
        show_mouse(NULL);

        // Remove the native Windows frame — we draw our own title bar.
        ui::chrome_remove_frame(this);

        m_backbuffer = create_bitmap(m_width, m_height);

        if (!m_backbuffer)
            return false;

        // Allow the OS close (Alt+F4, taskbar) to signal the app.
        LOCK_FUNCTION(close_button_handler);
        set_close_button_callback(close_button_handler);

        // --- Theme ---
        ui::theme_init();

        // --- Settings ---
        m_settings.load(SETTINGS_FILE);

        // --- Local game database ---
        m_game_db.open(GAME_DB_FILE);

        // --- SDK clients ---
        m_http = new lancommander::WinInetHttpClient();
        m_auth = new lancommander::AuthenticationClient(*m_http);
        m_connection = new lancommander::ConnectionClient(*m_http);
        m_games = new lancommander::GameClient(*m_http);
        m_library = new lancommander::LibraryClient(*m_http);
        m_media = new lancommander::MediaClient(*m_http);
        m_tools = new lancommander::ToolClient(*m_http);
        m_depot = new lancommander::DepotClient(*m_http);
        m_launcher = new lancommander::LauncherClient(*m_http);

        // Image loading (GDI+ for PNG/JPEG decode).
        image_decoder_init();
        m_image_cache = new ui::ImageCache(*m_media, MEDIA_DIR);

        // Restore saved connection state.
        if (!m_settings.authentication.server_address.empty())
        {
            log_info("Server address: %s", m_settings.authentication.server_address.c_str());
            m_connection->set_server_address(m_settings.authentication.server_address);
        }

        if (m_settings.authentication.offline_mode)
        {
            // Offline mode — skip validation, go straight to library.
            log_info("Offline mode enabled, skipping login");
            m_connection->enable_offline_mode();
            if (!m_settings.launcher.username.empty())
                m_user_alias = m_settings.launcher.username;
            m_current_screen = Screen::Library;
        }
        else if (!m_settings.authentication.token.access_token.empty())
        {
            m_connection->set_access_token(m_settings.authentication.token.access_token);

            // Validate token — if still valid, skip login.
            log_info("Validating saved token");
            auto valid = m_auth->validate();

            if (valid && valid.value)
            {
                log_info("Token valid, skipping login");

                // Fetch user alias for display.
                lancommander::ProfileClient profile(*m_http);
                auto alias = profile.get_alias();
                if (alias && !alias.value.empty())
                    m_user_alias = alias.value;
                else if (!m_settings.launcher.username.empty())
                    m_user_alias = m_settings.launcher.username;

                m_connection->connect();
                m_current_screen = Screen::Library;
            }
            else
            {
                // Token invalid — try refresh before falling back to login.
                log_info("Token invalid, attempting refresh");
                lancommander::AuthToken current;
                current.access_token = m_settings.authentication.token.access_token;
                current.refresh_token = m_settings.authentication.token.refresh_token;

                auto refreshed = m_auth->refresh(current);
                if (refreshed)
                {
                    log_info("Token refreshed successfully");
                    m_connection->set_access_token(refreshed.value.access_token);
                    m_settings.authentication.token.access_token = refreshed.value.access_token;
                    m_settings.authentication.token.refresh_token = refreshed.value.refresh_token;

                    lancommander::ProfileClient profile(*m_http);
                    auto alias = profile.get_alias();
                    if (alias && !alias.value.empty())
                        m_user_alias = alias.value;
                    else if (!m_settings.launcher.username.empty())
                        m_user_alias = m_settings.launcher.username;

                    m_connection->connect();
                    m_current_screen = Screen::Library;
                }
                else
                {
                    log_warn("Token refresh failed, showing login screen");
                }
            }
        }

        // Final fallback: use saved username if alias is still empty.
        if (m_user_alias.empty() && !m_settings.launcher.username.empty())
            m_user_alias = m_settings.launcher.username;

        log_info("Init complete, starting on %s screen (alias=%s)",
                 m_current_screen == Screen::Library ? "Library" : "Login",
                 m_user_alias.c_str());
        return true;
    }

    int App::run()
    {
        ui::InputState input;

        while (!m_quit)
        {
            // --- Input: drain all events once per frame ---
            input.poll();

            // --- Apply pending resize (set by WndProc) ---
            apply_pending_resize();

            // OS close request (taskbar Close, WM_CLOSE).
            if (s_close_requested)
                m_quit = true;

            // Alt+F4
            if (input.key_pressed(KEY_F4) && (key_shifts & KB_ALT_FLAG))
                m_quit = true;

            // Global ESC handling
            if (input.key_pressed(KEY_ESC))
            {
                if (m_current_screen == Screen::GameDetail ||
                    m_current_screen == Screen::Downloads ||
                    m_current_screen == Screen::Settings)
                    m_current_screen = Screen::Library;
                else
                    m_quit = true;
            }

            // --- Tick download queue ---
            m_downloads.tick(*m_games, *m_library);

            // --- Reset per-frame decode budget ---
            m_image_cache->begin_frame();

            // --- Clear ---
            clear_to_color(m_backbuffer, ui::theme().bg);

            // --- Draw current screen ---
            switch (m_current_screen)
            {
                case Screen::Login:
                    ui::screen_login_draw(*this, input);
                    break;
                case Screen::Library:
                    ui::screen_library_draw(*this, input);
                    break;
                case Screen::GameDetail:
                    ui::screen_game_detail_draw(*this, input);
                    break;
                case Screen::Downloads:
                    ui::screen_downloads_draw(*this, input);
                    break;
                case Screen::Settings:
                    ui::screen_settings_draw(*this, input);
                    break;
            }

            // --- Footer bar (drawn on top of screen content) ---
            if (m_current_screen == Screen::Library || m_current_screen == Screen::GameDetail ||
                m_current_screen == Screen::Downloads || m_current_screen == Screen::Settings)
                ui::window_footer_draw(*this, input);

            // --- Window chrome (custom title bar, drawn on top) ---
            if (ui::window_chrome_draw(*this, input))
                m_quit = true;

            // --- Mouse cursor (drawn on backbuffer so it isn't overwritten) ---
            {
                // 12x19 arrow cursor bitmap (0=transparent, 1=black, 2=white)
                static const unsigned char cursor[19][12] = {
                    {1,0,0,0,0,0,0,0,0,0,0,0},
                    {1,1,0,0,0,0,0,0,0,0,0,0},
                    {1,2,1,0,0,0,0,0,0,0,0,0},
                    {1,2,2,1,0,0,0,0,0,0,0,0},
                    {1,2,2,2,1,0,0,0,0,0,0,0},
                    {1,2,2,2,2,1,0,0,0,0,0,0},
                    {1,2,2,2,2,2,1,0,0,0,0,0},
                    {1,2,2,2,2,2,2,1,0,0,0,0},
                    {1,2,2,2,2,2,2,2,1,0,0,0},
                    {1,2,2,2,2,2,2,2,2,1,0,0},
                    {1,2,2,2,2,2,2,2,2,2,1,0},
                    {1,2,2,2,2,2,2,2,2,2,2,1},
                    {1,2,2,2,2,2,2,1,1,1,1,1},
                    {1,2,2,2,1,2,2,1,0,0,0,0},
                    {1,2,2,1,0,1,2,2,1,0,0,0},
                    {1,2,1,0,0,1,2,2,1,0,0,0},
                    {1,1,0,0,0,0,1,2,2,1,0,0},
                    {1,0,0,0,0,0,1,2,2,1,0,0},
                    {0,0,0,0,0,0,0,1,1,0,0,0},
                };
                int mx = input.mouse.x;
                int my = input.mouse.y;
                int col_b = makecol(0, 0, 0);
                int col_w = makecol(255, 255, 255);
                for (int row = 0; row < 19; ++row)
                {
                    int py = my + row;
                    if (py < 0 || py >= m_height) continue;
                    for (int col = 0; col < 12; ++col)
                    {
                        int px = mx + col;
                        if (px < 0 || px >= m_width) continue;
                        if (cursor[row][col] == 1)
                            putpixel(m_backbuffer, px, py, col_b);
                        else if (cursor[row][col] == 2)
                            putpixel(m_backbuffer, px, py, col_w);
                    }
                }
            }

            // --- Flip ---
            // Blit directly to the window DC so we aren't limited by
            // Allegro's fixed-size screen bitmap after a resize.
#ifdef ALLEGRO_WINDOWS
            {
                HWND hwnd = win_get_window();
                HDC hdc = GetDC(hwnd);
                blit_to_hdc(m_backbuffer, hdc, 0, 0, 0, 0, m_width, m_height);
                ReleaseDC(hwnd, hdc);
            }
#else
            blit(m_backbuffer, screen, 0, 0, 0, 0, m_width, m_height);
#endif

            // Simple frame limiter (~30 FPS to keep CPU usage low)
            rest(33);
        }

        return 0;
    }

    void App::shutdown()
    {
        log_shutdown();

        // Save settings before exit.
        m_settings.authentication.server_address = m_connection->get_server_address();
        m_settings.authentication.token.access_token = m_connection->get_access_token();
        m_settings.save(SETTINGS_FILE);

        if (m_backbuffer)
        {
            destroy_bitmap(m_backbuffer);
            m_backbuffer = NULL;
        }

        image_decoder_shutdown();

        allegro_exit();
    }

    // --- Accessors ---

    lancommander::IHttpClient &App::http() { return *m_http; }
    lancommander::AuthenticationClient &App::auth() { return *m_auth; }
    lancommander::ConnectionClient &App::connection() { return *m_connection; }
    lancommander::GameClient &App::games() { return *m_games; }
    lancommander::LibraryClient &App::library() { return *m_library; }
    lancommander::MediaClient &App::media() { return *m_media; }
    lancommander::ToolClient &App::tools() { return *m_tools; }
    lancommander::DepotClient &App::depot() { return *m_depot; }
    lancommander::LauncherClient &App::launcher_client() { return *m_launcher; }

    Settings &App::settings() { return m_settings; }

    BITMAP *App::backbuffer() { return m_backbuffer; }
    int App::screen_width() const { return m_width; }
    int App::screen_height() const { return m_height; }

    void App::switch_screen(Screen s) { m_current_screen = s; }
    Screen App::current_screen() const { return m_current_screen; }

    void App::set_selected_game(const std::string &id) { m_selected_game = id; }
    std::string App::selected_game() const { return m_selected_game; }

    void App::set_user_alias(const std::string &alias) { m_user_alias = alias; }
    const std::string &App::user_alias() const { return m_user_alias; }

    std::vector<lancommander::Game> &App::game_cache() { return m_game_cache; }
    std::vector<lancommander::DepotGame> &App::depot_cache() { return m_depot_cache; }
    ui::ImageCache &App::image_cache() { return *m_image_cache; }
    DownloadQueue &App::downloads() { return m_downloads; }
    GameDatabase &App::game_db() { return m_game_db; }

    LibraryTab App::library_tab() const { return m_library_tab; }
    void App::set_library_tab(LibraryTab tab) { m_library_tab = tab; }

    void App::quit() { m_quit = true; }
    bool App::should_quit() const { return m_quit; }

    void App::request_resize(int new_w, int new_h)
    {
        m_pending_width = new_w;
        m_pending_height = new_h;
        m_resize_pending = true;
    }

    void App::apply_pending_resize()
    {
        if (!m_resize_pending)
            return;
        m_resize_pending = false;

        int new_w = m_pending_width;
        int new_h = m_pending_height;

        if (new_w == m_width && new_h == m_height)
            return;
        if (new_w <= 0 || new_h <= 0)
            return;

        BITMAP *new_buf = create_bitmap(new_w, new_h);
        if (!new_buf)
            return;

        destroy_bitmap(m_backbuffer);
        m_backbuffer = new_buf;
        m_width = new_w;
        m_height = new_h;
    }

} // namespace launcher
