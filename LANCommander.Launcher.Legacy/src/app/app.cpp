#include "app/app.h"
#include "version.h"
#include "gfx/gfx.h"
#include "app/logger.h"
#include "app/fs.h"
#include "app/paths.h"
#include "app/time_util.h"
#include "ui/input.h"
#include "ui/theme.h"
#include "ui/image_decoder.h"
#include "ui/icons.h"
#include "ui/window_chrome.h"
#include "ui/chrome_platform.h"
#include "ui/screen_login.h"
#include "ui/screen_server_select.h"
#include "ui/screen_library.h"
#include "ui/screen_depot.h"
#include "ui/screen_depot_browse.h"
#include "ui/screen_game_detail.h"
#include "ui/screen_downloads.h"
#include "ui/screen_settings.h"

#include <algorithm>
#include <cctype>

// The HTTP backend, resolved through the include path CMake sets for
// whichever one it linked. One typedef rather than an ifdef at each of the
// three construction sites below.
#ifdef __DJGPP__
#include "watt32_http_client.h"
typedef lancommander::Watt32HttpClient PlatformHttpClient;
#else
#include "wininet_http_client.h"
typedef lancommander::WinInetHttpClient PlatformHttpClient;
#endif

namespace launcher
{

    static std::string data_dir()      { return app_path("Data"); }
    static std::string settings_file() { return app_path("Data\\Settings.yml"); }
    static std::string game_db_file()  { return app_path("Data\\LANCommander.db"); }
    static std::string log_dir()       { return app_path("Data\\Logs"); }
    static std::string media_dir()     { return app_path("Data\\Media"); }

    App::App()
        : m_http(NULL), m_auth(NULL), m_connection(NULL), m_games(NULL), m_library(NULL), m_media(NULL), m_tools(NULL), m_depot(NULL), m_launcher(NULL), m_play_sessions_client(NULL), m_image_cache(NULL), m_prefetch_http(NULL), m_prefetch_media(NULL), m_prefetch(NULL), m_art_http(NULL), m_art_games(NULL), m_art_fetcher(NULL), m_current_screen(Screen::Login), m_library_tab(LibraryTab::Library), m_depot_filter_kind(DepotFilterKind::None), m_has_avatar(false), m_overlay_active(false), m_quit(false), m_resize_pending(false), m_pending_width(0), m_pending_height(0)
    {
    }

    App::~App()
    {
        // Workers must be stopped before the clients they use are freed.
        delete m_art_fetcher;
        delete m_art_games;
        delete m_art_http;

        delete m_prefetch;
        delete m_prefetch_media;
        delete m_prefetch_http;

        delete m_image_cache;
        delete m_play_sessions_client;
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
        // Create the Data directory structure.
        fs_mkdir(data_dir());

        log_init(log_dir().c_str());

        // --- Display ---
        if (!gfx::init_display("LANCommander", width, height))
        {
            log_error("gfx::init_display() failed");
            return false;
        }

        // Go frameless and install the resize/drag hit test — we draw our own
        // title bar. What this does depends on the backend.
        ui::chrome_platform_init(this);

        // --- Theme ---
        if (!ui::theme_init())
        {
            // On DOS this is nearly always the long filename question:
            // "Inter-Regular.ttf" is not an 8.3 name, so without DOSLFN the
            // file is there and cannot be opened by that name.
            log_error("Font init failed: could not load %s",
                      app_path("assets/fonts/Inter-Regular.ttf").c_str());
            return false;
        }

        // --- Settings ---
        m_settings.load(settings_file().c_str());

        // --- Local game database ---
        m_game_db.open(game_db_file().c_str());

        // --- SDK clients ---
        m_http = new PlatformHttpClient();
        m_http->set_client_version(LC_LAUNCHER_VERSION);
        m_auth = new lancommander::AuthenticationClient(*m_http);
        m_connection = new lancommander::ConnectionClient(*m_http);
        m_games = new lancommander::GameClient(*m_http);
        m_library = new lancommander::LibraryClient(*m_http);
        m_media = new lancommander::MediaClient(*m_http);
        m_tools = new lancommander::ToolClient(*m_http);
        m_depot = new lancommander::DepotClient(*m_http);
        m_launcher = new lancommander::LauncherClient(*m_http);
        m_play_sessions_client = new lancommander::PlaySessionClient(*m_http);

        // Image loading (GDI+ for PNG/JPEG decode).
        image_decoder_init();
        m_image_cache = new ui::ImageCache(*m_media, media_dir());

        m_prefetch_http = new PlatformHttpClient();
        m_prefetch_http->set_client_version(LC_LAUNCHER_VERSION);
        m_prefetch_media = new lancommander::MediaClient(*m_prefetch_http);
        m_prefetch = new MediaPrefetch(*m_prefetch_http, *m_prefetch_media, media_dir());
        m_image_cache->set_prefetch(m_prefetch);

        m_art_http = new PlatformHttpClient();
        m_art_http->set_client_version(LC_LAUNCHER_VERSION);
        m_art_games = new lancommander::GameClient(*m_art_http);
        m_art_fetcher = new GameArtFetcher(*m_art_http, *m_art_games);

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

                refresh_profile();

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

                    refresh_profile();

                    m_connection->connect();
                    m_current_screen = Screen::Library;
                }
                else
                {
                    log_warn("Token refresh failed, showing login screen");
                }
            }
        }


        if (m_current_screen == Screen::Login &&
            m_settings.authentication.server_address.empty())
            m_current_screen = Screen::ServerSelect;

        // Final fallback: use saved username if alias is still empty.
        if (m_user_alias.empty() && !m_settings.launcher.username.empty())
            m_user_alias = m_settings.launcher.username;

        const char *screen_name = "Login";
        switch (m_current_screen)
        {
        case Screen::ServerSelect: screen_name = "ServerSelect"; break;
        case Screen::Login:        screen_name = "Login"; break;
        case Screen::Library:      screen_name = "Library"; break;
        case Screen::Depot:        screen_name = "Depot"; break;
        case Screen::DepotBrowse:  screen_name = "DepotBrowse"; break;
        case Screen::GameDetail:   screen_name = "GameDetail"; break;
        case Screen::Downloads:    screen_name = "Downloads"; break;
        case Screen::Settings:     screen_name = "Settings"; break;
        }

        log_info("Init complete, starting on %s screen (alias=%s, surface=%dx%d)",
                 screen_name, m_user_alias.c_str(), screen_width(), screen_height());
        return true;
    }

    int App::run()
    {
        ui::InputState input;

        while (!m_quit)
        {
            const unsigned int frame_start = gfx::ticks_ms();

            // --- Input: drain all events once per frame ---
            input.poll();

            // --- Apply pending resize ---
            // The Win32 backend reports this through its WndProc; the SDL
            // backend has no WndProc and reports it on the InputState.
            if (input.resized)
                request_resize(input.resize_w, input.resize_h);

            apply_pending_resize();

            // OS close request (taskbar Close, WM_CLOSE).
            if (input.quit_requested)
                m_quit = true;

            // Alt+F4
            if (input.key_pressed(ui::Key::F4) && input.mod_down(ui::ModAlt))
                m_quit = true;

            if (input.key_pressed(ui::Key::Escape) && !m_overlay_active)
            {
                if (m_current_screen == Screen::GameDetail ||
                    m_current_screen == Screen::Downloads ||
                    m_current_screen == Screen::Settings ||
                    m_current_screen == Screen::DepotBrowse)
                    go_back();
                else
                    m_quit = true;
            }

            // --- Tick download queue ---
            m_downloads.tick(*m_games, *m_library);

            m_prefetch->tick(m_connection->get_server_address(),
                             m_connection->get_access_token());

            m_art_fetcher->tick(m_connection->get_server_address(),
                                m_connection->get_access_token());

            // --- Reset per-frame decode budget ---
            m_image_cache->begin_frame();

            // --- Clear ---
            gfx::clear(gfx::backbuffer(), ui::theme().bg);

            // --- Draw current screen ---
            //
            // Gated, not raw. The title bar, footer and profile dropdown are
            // drawn after this but sit on top of it; without the gate a click
            // aimed at one of them also landed on whatever the screen had
            // drawn underneath. See ui::chrome_gate().
            const ui::InputState screen_input = ui::chrome_gate(*this, input);

            switch (m_current_screen)
            {
                case Screen::ServerSelect:
                    ui::screen_server_select_draw(*this, screen_input);
                    break;
                case Screen::Login:
                    ui::screen_login_draw(*this, screen_input);
                    break;
                case Screen::Library:
                    ui::screen_library_draw(*this, screen_input);
                    break;
                case Screen::Depot:
                    ui::screen_depot_draw(*this, screen_input);
                    break;
                case Screen::DepotBrowse:
                    ui::screen_depot_browse_draw(*this, screen_input);
                    break;
                case Screen::GameDetail:
                    ui::screen_game_detail_draw(*this, screen_input);
                    break;
                case Screen::Downloads:
                    ui::screen_downloads_draw(*this, screen_input);
                    break;
                case Screen::Settings:
                    ui::screen_settings_draw(*this, screen_input);
                    break;
            }

            // --- Footer bar (drawn on top of screen content) ---
            if (ui::footer_visible(*this))
                ui::window_footer_draw(*this, input);

            // --- Window chrome (custom title bar, drawn on top) ---
            if (ui::window_chrome_draw(*this, input))
                m_quit = true;

            // --- Flip ---
            gfx::present();

            // Frame limiter (~30 FPS to keep CPU usage low). Subtracts the
            // work already done this frame, which the old fixed rest(33)
            // never did — so this is the first time it has actually been a
            // 30 FPS cap rather than "30 FPS minus however long drawing took".
            {
                const unsigned int FRAME_MS = 33;
                unsigned int elapsed = gfx::ticks_ms() - frame_start;
                if (elapsed < FRAME_MS)
                    gfx::delay_ms(FRAME_MS - elapsed);
            }
        }

        return 0;
    }

    void App::shutdown()
    {
        log_shutdown();

        // Save settings before exit.
        m_settings.authentication.server_address = m_connection->get_server_address();
        m_settings.authentication.token.access_token = m_connection->get_access_token();
        m_settings.save(settings_file().c_str());

        ui::icons_shutdown();
        ui::theme_shutdown();
        image_decoder_shutdown();

        gfx::shutdown_display();
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

    gfx::Surface *App::backbuffer() { return gfx::backbuffer(); }
    int App::screen_width() const { return gfx::display_width(); }
    int App::screen_height() const { return gfx::display_height(); }

    void App::refresh_profile()
    {
        lancommander::ProfileClient profile(*m_http);

        auto me = profile.get();
        if (me)
        {
            m_user_id = me.value.id;

            if (!me.value.alias.empty())
                m_user_alias = me.value.alias;
            else if (!me.value.user_name.empty())
                m_user_alias = me.value.user_name;
        }

        if (m_user_alias.empty() && !m_settings.launcher.username.empty())
            m_user_alias = m_settings.launcher.username;

        m_avatar_path = app_path("Data\\avatar");
        m_has_avatar = false;

        auto avatar = profile.download_avatar(m_avatar_path);
        if (avatar && avatar.value)
            m_has_avatar = true;
        else
            log_info("No profile avatar available for this user");
    }

    bool App::has_avatar() const { return m_has_avatar; }
    const std::string &App::avatar_path() const { return m_avatar_path; }

    const std::string &App::user_id() const { return m_user_id; }

    void App::switch_screen(Screen s)
    {
        if (s == m_current_screen)
            return;

        if (!m_nav_stack.empty() && m_nav_stack.back() == s)
        {
            m_nav_stack.pop_back();
        }
        else
        {
            m_nav_stack.push_back(m_current_screen);
            if (m_nav_stack.size() > 16)
                m_nav_stack.erase(m_nav_stack.begin());
        }

        m_current_screen = s;

        m_overlay_active = false;
    }

    Screen App::current_screen() const { return m_current_screen; }

    void App::go_back()
    {
        m_overlay_active = false;

        if (m_nav_stack.empty())
        {
            m_current_screen = Screen::Library;
            return;
        }

        m_current_screen = m_nav_stack.back();
        m_nav_stack.pop_back();
    }

    void App::set_overlay_active(bool active) { m_overlay_active = active; }
    bool App::overlay_active() const { return m_overlay_active; }

    void App::set_selected_game(const std::string &id) { m_selected_game = id; }
    std::string App::selected_game() const { return m_selected_game; }

    void App::set_user_alias(const std::string &alias) { m_user_alias = alias; }
    const std::string &App::user_alias() const { return m_user_alias; }

    DepotData &App::depot_data() { return m_depot_data; }
    LibraryData &App::library_data() { return m_library_data; }

    void App::invalidate_depot() { m_depot_data.state = LoadState::Empty; }
    void App::invalidate_library()
    {
        m_library_data.state = LoadState::Empty;
        m_game_art.state = LoadState::Empty;

        if (m_art_fetcher)
            m_art_fetcher->clear();

        m_play_sessions.state = LoadState::Empty;
    }

    namespace
    {
        bool iless(const std::string &a, const std::string &b)
        {
            size_t len = a.size() < b.size() ? a.size() : b.size();
            for (size_t i = 0; i < len; ++i)
            {
                int ca = std::tolower((unsigned char)a[i]);
                int cb = std::tolower((unsigned char)b[i]);
                if (ca != cb)
                    return ca < cb;
            }
            return a.size() < b.size();
        }

        const std::string &sort_key(const lancommander::DepotGame &g)
        {
            return g.sort_title.empty() ? g.title : g.sort_title;
        }

        const std::string &sort_key(const lancommander::Game &g)
        {
            return g.sort_title.empty() ? g.title : g.sort_title;
        }

        // Pull the four drawable media ids out of a game record.
        GameArt art_from_media(const lancommander::Game &g)
        {
            GameArt art;

            for (size_t m = 0; m < g.media.size(); ++m)
            {
                const std::string &type = g.media[m].type;
                const std::string &id = g.media[m].id;

                if (type == "Icon" && art.icon.empty()) art.icon = id;
                else if (type == "Cover" && art.cover.empty()) art.cover = id;
                else if (type == "Background" && art.background.empty()) art.background = id;
                else if (type == "Logo" && art.logo.empty()) art.logo = id;
            }

            if (art.cover.empty())
                art.cover = g.cover_media_id;

            art.complete = true;
            return art;
        }

        bool is_top_level(lancommander::GameType t)
        {
            return t == lancommander::GameType::MainGame
                || t == lancommander::GameType::StandaloneExpansion
                || t == lancommander::GameType::StandaloneMod;
        }
    } // namespace

    void App::ensure_depot_loaded()
    {
        if (m_depot_data.state != LoadState::Empty)
            return;

        // Synchronous, like every other network call in the launcher. The
        // state is set before the call so a re-entrant frame cannot start a
        // second fetch.
        m_depot_data.state = LoadState::Loading;

        auto result = m_depot->get();
        if (!result)
        {
            m_depot_data.error = result.error;
            m_depot_data.state = LoadState::Failed;
            return;
        }

        std::vector<lancommander::DepotGame> filtered;
        for (size_t i = 0; i < result.value.games.size(); ++i)
        {
            if (is_top_level(result.value.games[i].type))
                filtered.push_back(result.value.games[i]);
        }

        std::sort(filtered.begin(), filtered.end(),
                  [](const lancommander::DepotGame &a, const lancommander::DepotGame &b)
                  { return iless(sort_key(a), sort_key(b)); });

        m_depot_data.games.swap(filtered);
        m_depot_data.error.clear();
        m_depot_data.state = LoadState::Loaded;
        ++m_depot_data.revision;
    }

    void App::ensure_library_loaded()
    {
        if (m_library_data.state != LoadState::Empty)
            return;

        ensure_depot_loaded();

        m_library_data.state = LoadState::Loading;

        auto result = m_library->get_games();
        if (!result)
        {
            m_library_data.error = result.error;
            m_library_data.state = LoadState::Failed;
            return;
        }

        m_game_art.by_game_id.clear();

        for (size_t i = 0; i < result.value.size(); ++i)
        {
            const lancommander::Game &src = result.value[i];
            m_game_art.by_game_id[src.id] = art_from_media(src);
        }

        for (size_t d = 0; d < m_depot_data.games.size(); ++d)
        {
            const lancommander::DepotGame &dg = m_depot_data.games[d];
            if (dg.cover.id.empty())
                continue;

            GameArt &art = m_game_art.by_game_id[dg.id];
            if (art.cover.empty())
                art.cover = dg.cover.id;
        }

        m_game_art.state = LoadState::Loaded;

        std::vector<lancommander::Game> lib;
        for (size_t i = 0; i < result.value.size(); ++i)
        {
            if (!is_top_level(result.value[i].type))
                continue;

            lancommander::Game g = result.value[i];

            if (g.cover_media_id.empty())
            {
                const std::map<std::string, GameArt>::const_iterator ai =
                    m_game_art.by_game_id.find(g.id);
                if (ai != m_game_art.by_game_id.end())
                    g.cover_media_id = ai->second.cover;
            }

            InstalledGame local;
            if (m_game_db.find(g.id, &local))
                g.install_directory = local.install_directory;

            lib.push_back(g);
        }

        std::sort(lib.begin(), lib.end(),
                  [](const lancommander::Game &a, const lancommander::Game &b)
                  { return iless(sort_key(a), sort_key(b)); });

        m_library_data.games.swap(lib);
        m_library_data.error.clear();
        m_library_data.state = LoadState::Loaded;
        ++m_library_data.revision;
    }

    void App::ensure_play_sessions_loaded()
    {
        if (m_play_sessions.state != LoadState::Empty)
            return;

        m_play_sessions.state = LoadState::Loading;
        m_play_sessions.last_played.clear();
        m_play_sessions.total_seconds.clear();

        auto result = m_play_sessions_client->get();

        if (result)
        {
            for (size_t i = 0; i < result.value.size(); ++i)
            {
                const lancommander::PlaySession &ps = result.value[i];

                if (ps.game_id.empty() || ps.end.empty())
                    continue;

                const long long end = (long long)iso8601_to_time_t(ps.end);
                if (end <= 0)
                    continue;

                long long &last = m_play_sessions.last_played[ps.game_id];
                if (end > last)
                    last = end;

                const long long start = (long long)iso8601_to_time_t(ps.start);
                if (start > 0 && end > start)
                    m_play_sessions.total_seconds[ps.game_id] += (end - start);
            }

            m_play_sessions.error.clear();
        }
        else
        {
            m_play_sessions.error = result.error;
            log_warn("Could not load play sessions from the server: %s",
                     result.error.c_str());
        }

        std::vector<std::pair<std::string, std::string> > local;
        m_game_db.last_played_all(&local);

        for (size_t i = 0; i < local.size(); ++i)
        {
            const long long end = (long long)iso8601_to_time_t(local[i].second);
            if (end <= 0)
                continue;

            long long &last = m_play_sessions.last_played[local[i].first];
            if (end > last)
                last = end;
        }

        for (size_t i = 0; i < local.size(); ++i)
        {
            const long long secs = m_game_db.total_play_seconds(local[i].first);
            if (secs > m_play_sessions.total_seconds[local[i].first])
                m_play_sessions.total_seconds[local[i].first] = secs;
        }

        m_play_sessions.state = LoadState::Loaded;
    }

    void App::recent_game_ids(int limit, std::vector<std::string> *out)
    {
        if (!out)
            return;
        out->clear();

        if (limit < 0)
            return;

        ensure_play_sessions_loaded();

        std::vector<std::pair<long long, std::string> > by_time;
        by_time.reserve(m_play_sessions.last_played.size());

        for (std::map<std::string, long long>::const_iterator it =
                 m_play_sessions.last_played.begin();
             it != m_play_sessions.last_played.end(); ++it)
        {
            by_time.push_back(std::make_pair(it->second, it->first));
        }

        std::sort(by_time.begin(), by_time.end(),
                  [](const std::pair<long long, std::string> &a,
                     const std::pair<long long, std::string> &b)
                  { return a.first > b.first; });

        for (size_t i = 0; i < by_time.size(); ++i)
        {
            if (limit > 0 && (int)i >= limit)
                break;
            out->push_back(by_time[i].second);
        }
    }

    long long App::last_played_at(const std::string &game_id)
    {
        ensure_play_sessions_loaded();

        std::map<std::string, long long>::const_iterator it =
            m_play_sessions.last_played.find(game_id);
        return (it == m_play_sessions.last_played.end()) ? 0 : it->second;
    }

    long long App::total_play_seconds(const std::string &game_id)
    {
        ensure_play_sessions_loaded();

        std::map<std::string, long long>::const_iterator it =
            m_play_sessions.total_seconds.find(game_id);
        return (it == m_play_sessions.total_seconds.end()) ? 0 : it->second;
    }

    void App::ensure_game_art_loaded()
    {
        if (m_game_art.state != LoadState::Empty)
            return;

        ensure_library_loaded();

        if (m_library_data.state == LoadState::Failed)
            m_game_art.state = LoadState::Failed;
    }

    GameArt App::game_art(const std::string &game_id) const
    {
        std::map<std::string, GameArt>::const_iterator it =
            m_game_art.by_game_id.find(game_id);
        if (it != m_game_art.by_game_id.end() && it->second.complete)
            return it->second;

        GameArt fetched;
        if (m_art_fetcher && m_art_fetcher->resolved(game_id, &fetched))
            return fetched;

        return (it != m_game_art.by_game_id.end()) ? it->second : GameArt();
    }

    void App::request_game_art(const std::string &game_id)
    {
        if (game_id.empty() || !m_art_fetcher)
            return;

        const std::map<std::string, GameArt>::const_iterator it =
            m_game_art.by_game_id.find(game_id);
        if (it != m_game_art.by_game_id.end() && it->second.complete)
            return;

        m_art_fetcher->request(game_id);
    }

    ui::ImageCache &App::image_cache() { return *m_image_cache; }
    MediaPrefetch &App::media_prefetch() { return *m_prefetch; }
    DownloadQueue &App::downloads() { return m_downloads; }
    GameDatabase &App::game_db() { return m_game_db; }

    LibraryTab App::library_tab() const { return m_library_tab; }

    void App::set_depot_filter(DepotFilterKind kind, const std::string &value)
    {
        m_depot_filter_kind = kind;
        m_depot_filter_value = value;
    }

    DepotFilterKind App::depot_filter_kind() const { return m_depot_filter_kind; }
    const std::string &App::depot_filter_value() const { return m_depot_filter_value; }
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

        gfx::resize_display(m_pending_width, m_pending_height);
    }

} // namespace launcher
