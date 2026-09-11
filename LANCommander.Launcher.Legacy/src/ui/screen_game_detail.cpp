#include "ui/screen_game_detail.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "ui/widgets_overlay.h"
#include "ui/widgets_menu.h"
#include "ui/widgets_collection.h"
#include "app/game_menu.h"
#include "app/time_util.h"
#include "ui/icons.h"
#include "ui/image_cache.h"
#include "app/app.h"
#include "app/game_metadata.h"
#include "app/save_sync.h"

#include <lancommander/script/script_helper.h>
#include <lancommander/util/path.h>
#include "app/game_database.h"
#include "app/logger.h"

#include "gfx/gfx.h"

#include "app/fs.h"
#include "app/process.h"

#include <cstdio>
#include <cstring>

#ifdef _WIN32
// Only for act_browse_files below, which is the one action with no meaning
// on a platform that has no file manager.
#include <windows.h>
#include <shellapi.h>
#endif

namespace launcher
{
    namespace ui
    {

        // -----------------------------------------------------------------
        // Persistent state
        // -----------------------------------------------------------------
        static std::string s_last_game_id;
        static lancommander::Game s_game;
        static std::vector<lancommander::Action> s_actions;
        static std::string s_status_message;
        static gfx::Color s_status_color = gfx::rgb(0, 0, 0);

        // When the message was posted. It is a transient acknowledgement --
        // "Added to download queue", "Added to library" -- not a field of the
        // game, so it expires rather than sitting in the action bar for the
        // rest of the session.
        static unsigned int s_status_at_ms = 0;

        // How long one stays up. Long enough to read at a glance, short
        // enough that it is gone before the user goes looking for the stats
        // it shares a row with.
        static const unsigned int STATUS_LIFETIME_MS = 6000;

        // Post a status message. Through here rather than by assigning the
        // pair directly, so nothing can set a message without also stamping
        // it -- an unstamped one inherits the previous timestamp and expires
        // early, or immediately.
        static void set_status(const std::string &message, gfx::Color color)
        {
            s_status_message = message;
            s_status_color = color;
            s_status_at_ms = gfx::ticks_ms();
        }

        // True while the current message should be drawn.
        static bool status_visible()
        {
            if (s_status_message.empty())
                return false;
            return (gfx::ticks_ms() - s_status_at_ms) < STATUS_LIFETIME_MS;
        }

        // Scroll state
        static ScrollState s_scroll;

        // Running game tracking
        static std::string s_running_game_id;
        static void *s_process_handle = NULL; // app/process.h handle
        static bool s_is_running = false;
        static bool s_is_starting = false;

        // Local play session for the run in progress, "" when nothing is
        // being tracked.
        static std::string s_session_id;

        // -----------------------------------------------------------------
        // Modal dialog state
        // -----------------------------------------------------------------
        enum class ModalType { None, ActionSelect, InstallOptions };
        static ModalType s_modal = ModalType::None;

        // The action-bar dropdown.
        static MenuState s_menu;

        // Result of the per-game update check, refreshed on load.
        static bool s_update_available = false;

        // Width of the primary action split button. Named because the stats
        // beside it are positioned from its right edge.
        static const int SPLIT_BTN_W = 150;

        // The action bar's button wears the Avalonia `Large` class
        // (Padding 24,14). It used to be a flat 30px, which is the height of
        // an ordinary button and left the one control the page exists for
        // looking like the Cancel next to it.
        static int split_btn_h() { return button_height_large(); }

        // Screenshot strip geometry. Named because the page height has to
        // account for the strip before it is drawn.
        static const int MEDIA_ITEM_W = 384;
        static const int MEDIA_ITEM_H = 216;

        // Screenshot media ids for the current game, and the viewer.
        static std::vector<std::string> s_screenshots;
        static CarouselState s_media_carousel;
        static LightboxState s_lightbox;

        // Action selection dialog
        static std::vector<const lancommander::Action *> s_modal_actions;

        // Install options dialog
        static std::vector<lancommander::Game> s_addons;
        // Using char instead of bool because vector<bool> is a
        // special-cased bitfield that doesn't support references.
        static std::vector<char> s_addon_selected;
        static int s_install_dir_index = 0;
        static bool s_addons_loaded = false;
        static ScrollState s_install_scroll;

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------
        static void load_game(App &app)
        {
            s_status_message.clear();

            auto result = app.games().get(app.selected_game());

            if (result)
            {
                s_game = result.value;

                // Apply local install state from database.
                InstalledGame local;
                if (app.game_db().find(s_game.id, &local))
                    s_game.install_directory = local.install_directory;
            }
            else
            {
                s_game = lancommander::Game();
                s_game.title = "Error loading game";
                set_status(result.error, theme().error);
            }

            auto actions = app.games().get_actions(app.selected_game());

            if (actions)
                s_actions = actions.value;
            else
                s_actions.clear();

            // Is there a newer build than the one on disk?
            //
            // One request for one game, which is affordable HERE and is why
            // the library sidebar does not show this: there it would be one
            // request per row.
            s_update_available = false;
            if (!s_game.install_directory.empty())
            {
                InstalledGame local;
                if (app.game_db().find(s_game.id, &local) && !local.version.empty())
                {
                    auto upd = app.games().check_for_update(s_game.id, local.version);
                    if (upd)
                        s_update_available = upd.value;
                }
            }

            // Screenshots only.
            //
            // MediaRef.type is a string, but parse_media_ref normalises the
            // numeric wire form to the enum NAME, so comparing against
            // "Screenshot" is reliable. Videos are deliberately excluded:
            // there is no decoder in this stack, and a play button that does
            // nothing is worse than an absent one.
            s_screenshots.clear();
            for (size_t i = 0; i < s_game.media.size(); ++i)
                if (s_game.media[i].type == "Screenshot")
                    s_screenshots.push_back(s_game.media[i].id);

            s_media_carousel = CarouselState();
            s_lightbox = LightboxState();

            s_last_game_id = app.selected_game();
        }

        // Human-readable size of the newest archive, or "" when the game has
        // none. Only /api/Games/{id} returns archives, which is what this
        // screen loads — the list endpoint the library uses does not, which is
        // why this stat cannot be shown anywhere else.
        static std::string format_download_size(const lancommander::Game &game)
        {
            // Newest by created_on rather than archives[0]: the order the
            // server returns them in is not part of the contract, and the
            // timestamps are ISO-8601, so a string compare is a date compare.
            const lancommander::Archive *latest = NULL;
            for (size_t i = 0; i < game.archives.size(); ++i)
            {
                if (!latest || game.archives[i].created_on > latest->created_on)
                    latest = &game.archives[i];
            }

            if (!latest || latest->compressed_size <= 0)
                return std::string();

            const long long bytes = latest->compressed_size;

            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;

            char out[32];
            if (bytes >= (long long)gb)
                sprintf(out, "%.2f GB", bytes / gb);
            else if (bytes >= (long long)mb)
                sprintf(out, "%.2f MB", bytes / mb);
            else if (bytes >= (long long)kb)
                sprintf(out, "%.0f KB", bytes / kb);
            else
                sprintf(out, "%d B", (int)bytes);

            return std::string(out);
        }

        // A titled group of wrapped badges. Returns the height consumed, so
        // the sidebar can stack groups without each one re-deriving the wrap.
        //
        // Split into a measure and a draw because the page height has to be
        // known before anything is drawn — the same reason draw_text_wrap
        // takes a NULL surface.
        static const int BADGE_GAP = 6;
        static const int GROUP_GAP = 10;

        static int badge_group_height(int avail_w, const std::vector<std::string> &items)
        {
            if (items.empty())
                return 0;

            const int row_h = badge_height();
            int x = 0;
            int rows = 1;

            for (size_t i = 0; i < items.size(); ++i)
            {
                const int w = badge_width(items[i].c_str());
                if (x > 0 && x + w > avail_w)
                {
                    ++rows;
                    x = 0;
                }
                x += w + BADGE_GAP;
            }

            return text_height() + 6 + rows * (row_h + BADGE_GAP) + GROUP_GAP;
        }

        static int draw_badge_group(gfx::Surface *buf, int x0, int y0, int avail_w,
                                    const char *title,
                                    const std::vector<std::string> &items,
                                    const InputState &input)
        {
            if (items.empty())
                return 0;

            const int row_h = badge_height();

            draw_text(buf, x0, y0, theme().text_dim, title);

            int x = x0;
            int y = y0 + text_height() + 6;

            for (size_t i = 0; i < items.size(); ++i)
            {
                const int w = badge_width(items[i].c_str());

                if (x > x0 && x + w > x0 + avail_w)
                {
                    x = x0;
                    y += row_h + BADGE_GAP;
                }

                // Not interactive: there is no search-by-genre destination in
                // this launcher yet, and a capsule that highlights but does
                // nothing is worse than one that plainly does not.
                badge(buf, x, y, items[i].c_str(), false, input);

                x += w + BADGE_GAP;
            }

            return (y + row_h + BADGE_GAP + GROUP_GAP) - y0;
        }

        static std::string find_media_id(const lancommander::Game &game,
                                         const char *type)
        {
            for (size_t i = 0; i < game.media.size(); ++i)
            {
                if (game.media[i].type == type)
                    return game.media[i].id;
            }
            return std::string();
        }

        static std::string find_cover_id(const lancommander::Game &game)
        {
            if (!game.cover_media_id.empty())
                return game.cover_media_id;
            return find_media_id(game, "Cover");
        }

        static const lancommander::Action *pick_primary_action()
        {
            const lancommander::Action *best = NULL;
            for (size_t i = 0; i < s_actions.size(); ++i)
            {
                if (!s_actions[i].is_primary)
                    continue;
                if (!best || s_actions[i].sort_order < best->sort_order)
                    best = &s_actions[i];
            }
            if (best)
                return best;
            // Fallback: first action by sort order.
            for (size_t i = 0; i < s_actions.size(); ++i)
            {
                if (!best || s_actions[i].sort_order < best->sort_order)
                    best = &s_actions[i];
            }
            return best;
        }

        // -----------------------------------------------------------------
        // Launch / process management
        // -----------------------------------------------------------------
        static void normalize_slashes(std::string &s)
        {
            for (size_t i = 0; i < s.size(); ++i)
                if (s[i] == '/')
                    s[i] = '\\';
        }

        static bool is_absolute(const std::string &p)
        {
            if (p.size() >= 2 && p[1] == ':')
                return true;
            if (!p.empty() && (p[0] == '\\' || p[0] == '/'))
                return true;
            return false;
        }

        static std::string join_path(const std::string &dir, const std::string &rel)
        {
            if (dir.empty())
                return rel;
            std::string out = dir;
            if (out[out.size() - 1] != '\\' && out[out.size() - 1] != '/')
                out += '\\';
            out += rel;
            return out;
        }

        static void replace_var(std::string &s, const std::string &var,
                                const std::string &val)
        {
            if (var.empty())
                return;
            size_t pos = 0;
            while ((pos = s.find(var, pos)) != std::string::npos)
            {
                s.replace(pos, var.size(), val);
                pos += val.size();
            }
        }

        static std::string expand_action_string(const std::string &input,
                                                const std::string &install_dir,
                                                const std::string &server_addr,
                                                const std::map<std::string, std::string> &vars)
        {
            if (input.empty())
                return input;
            std::string s = input;
            replace_var(s, "{InstallDir}", install_dir);
            replace_var(s, "{ServerAddress}", server_addr);
            for (std::map<std::string, std::string>::const_iterator it = vars.begin();
                 it != vars.end(); ++it)
                replace_var(s, "{" + it->first + "}", it->second);
            return s;
        }

        // The context a lifecycle script runs in. Everything here is either
        // the game's own or a setting the user typed -- nothing is guessed,
        // and a field the chosen script type does not use is simply left
        // empty, which ScriptExecutionClient reads as "do not inject".
        static ScriptTarget script_target(App &app)
        {
            ScriptTarget target;
            target.game_id = s_game.id;
            target.title = s_game.title;
            target.install_dir = s_game.install_directory;
            normalize_slashes(target.install_dir);
            target.server_address = app.connection().get_server_address();

            if (!app.settings().games.install_directories.empty())
                target.default_install_dir = app.settings().games.install_directories[0];

            // The alias the GAME was configured for, not whoever is signed in
            // now -- which is what the .NET SDK injects, and what makes
            // AfterStop able to undo what BeforeStart did after a rename.
            target.player_alias = game_player_alias(target.install_dir, target.game_id);

            // Everything on this screen runs on the thread that draws, so a
            // breakpoint here has to pump its own frames.
            target.on_ui_thread = true;

            return target;
        }

        // Runs NameChange when the signed-in alias has drifted from the one
        // this game was set up with, and records the new one.
        //
        // The .NET SDK writes the alias file from INSIDE its name-change
        // method, so a game with no NameChange.ps1 never gets one recorded and
        // its $PlayerAlias stays empty forever. That is a bug rather than a
        // contract: BeforeStart is a different script, and a game may well have
        // it without a NameChange. So the file is written whenever the alias
        // moves; only the script is conditional on existing.
        static void sync_player_alias(App &app)
        {
            const std::string current = app.user_alias();
            if (current.empty() || s_game.id.empty())
                return;

            std::string install_dir = s_game.install_directory;
            if (install_dir.empty())
                return;
            normalize_slashes(install_dir);

            const std::string stored = game_player_alias(install_dir, s_game.id);
            if (stored == current)
                return;

            ScriptTarget target = script_target(app);
            target.old_player_alias = stored;
            target.new_player_alias = current;

            app.script_host().run(lancommander::ScriptType::NameChange, target, NULL);

            set_game_player_alias(install_dir, s_game.id, current);
        }

        // Allocates a key for this install if it needs one, and runs
        // KeyChange so the game picks it up.
        //
        // The order of the three guards matters and is upstream's:
        //
        //   * offline -> do nothing. A key cannot be allocated without the
        //     server, and running KeyChange with an empty key would rewrite a
        //     working config with a blank serial.
        //   * no KeyChange script -> do nothing. Allocating a key for a game
        //     that has no way to apply it just burns one out of the pool.
        //   * a key already tracked -> do nothing. THE LOCAL FILE WINS. The
        //     server allocates partly on MAC address, which is not stable
        //     between launches on every adapter, so asking again on every run
        //     hands the game a different key each time.
        static void sync_allocated_key(App &app)
        {
            if (s_game.id.empty() || s_game.install_directory.empty())
                return;

            if (!app.connection().is_connected() ||
                app.connection().is_offline_mode())
                return;

            std::string install_dir = s_game.install_directory;
            normalize_slashes(install_dir);

            const std::string script_path = lancommander::script::script_file_path(
                install_dir, s_game.id, lancommander::ScriptType::KeyChange);

            if (script_path.empty() || !lancommander::path::exists(script_path))
                return;

            if (!game_key(install_dir, s_game.id).empty())
                return;

            lancommander::Result<std::string> allocated =
                app.keys().get_allocated(s_game.id);

            if (!allocated || allocated.value.empty())
            {
                // A game with a KeyChange script and no key to give it is
                // worth saying out loud: it will start, and it will start
                // unregistered.
                log_warn("%s has a key change script but the server allocated "
                         "no key", s_game.title.c_str());
                return;
            }

            // Recorded BEFORE the script runs, matching the SDK: if the script
            // fails halfway the key is still spent, and forgetting it here
            // would allocate a second one on the next launch.
            set_game_key(install_dir, s_game.id, allocated.value);

            ScriptTarget target = script_target(app);
            target.allocated_key = allocated.value;

            app.script_host().run(lancommander::ScriptType::KeyChange, target, NULL);
        }

        static SaveSyncContext save_context(App &app)
        {
            SaveSyncContext ctx;
            ctx.saves = &app.saves();
            ctx.scripts = &app.script_host();
            ctx.game_id = s_game.id;
            ctx.title = s_game.title;
            ctx.install_dir = s_game.install_directory;
            normalize_slashes(ctx.install_dir);
            ctx.server_address = app.connection().get_server_address();

            if (!app.settings().games.install_directories.empty())
                ctx.default_install_dir = app.settings().games.install_directories[0];

            ctx.on_ui_thread = true;

            return ctx;
        }

        // Pulls the newest cloud save down before the game starts.
        //
        // Best effort by design: a save that cannot be fetched must not stop
        // someone playing. The alternative -- refusing to launch because the
        // server is busy -- is worse than starting from a local save.
        static void download_saves(App &app)
        {
            if (s_game.id.empty() || s_game.install_directory.empty())
                return;
            if (!app.connection().is_connected() ||
                app.connection().is_offline_mode())
                return;

            const SaveSyncResult result = save_download(save_context(app));

            if (!result.ok && !result.error.empty())
                log_warn("Could not restore saves for %s: %s",
                         s_game.title.c_str(), result.error.c_str());
        }

        static void upload_saves(App &app)
        {
            if (s_game.id.empty() || s_game.install_directory.empty())
                return;
            if (!app.connection().is_connected() ||
                app.connection().is_offline_mode())
                return;

            const SaveSyncResult result = save_upload(save_context(app));

            if (!result.ok && !result.error.empty())
                log_warn("Could not upload saves for %s: %s",
                         s_game.title.c_str(), result.error.c_str());
        }

        // Defined below; a RunWrapper owns the whole play session inside
        // launch_action, so it needs them before they appear.
        static void session_started(App &app);
        static void session_stopped(App &app);

        static bool launch_action(App &app, const lancommander::Action &action,
                                  std::string *error_out)
        {
            std::string install_dir = s_game.install_directory;
            normalize_slashes(install_dir);

            // Reconcile the game's recorded alias with the signed-in one
            // BEFORE BeforeStart, exactly as the .NET launcher does on every
            // launch: NameChange is what rewrites the config a game keeps its
            // player name in, and BeforeStart then reads the updated value.
            sync_player_alias(app);

            // Then the key, then the cloud save -- upstream's order, and the
            // only one that works: KeyChange rewrites the game's config, and a
            // downloaded save may replace that very file, so the save has to
            // land after the key and before the game reads either.
            sync_allocated_key(app);
            download_saves(app);

            // BeforeStart is where a game writes the config that carries the
            // player's name, so it has to finish before the process starts
            // rather than race it. A failure stops the launch: starting the
            // game anyway would run it against a half-written config.
            if (!app.script_host().run(lancommander::ScriptType::BeforeStart,
                                       script_target(app), NULL))
            {
                if (error_out)
                    *error_out = "BeforeStart script failed - see the script console";
                return false;
            }
            std::string server_addr = app.connection().get_server_address();

            std::string path = expand_action_string(action.path, install_dir,
                                                    server_addr, action.variables);
            std::string args = expand_action_string(action.arguments, install_dir,
                                                    server_addr, action.variables);
            std::string cwd = expand_action_string(action.working_directory, install_dir,
                                                   server_addr, action.variables);

            normalize_slashes(path);
            if (!is_absolute(path))
                path = join_path(install_dir, path);
            if (cwd.empty())
                cwd = install_dir;
            else
            {
                normalize_slashes(cwd);
                if (!is_absolute(cwd))
                    cwd = join_path(install_dir, cwd);
            }

            // --- RunWrapper ---
            //
            // A redistributable can own the launch: umu, a compatibility
            // shim, a no-CD loader. Its script is handed the executable, the
            // arguments and the working directory, and starts the game itself.
            //
            // It does not return until the game has exited -- which is exactly
            // the shape DOS already has, so the session bookkeeping happens
            // here rather than being polled for by a handle that will never
            // exist.
            {
                const std::vector<std::string> wrappers =
                    ScriptHost::run_wrapper_redistributables(install_dir, s_game.id);

                for (std::size_t i = 0; i < wrappers.size(); ++i)
                {
                    ScriptTarget target = script_target(app);
                    lancommander::ScriptRun run;

                    session_started(app);

                    const bool ok = app.script_host().run_run_wrapper(
                        target, wrappers[i], path, args, cwd, &run);

                    if (!run.ran)
                    {
                        // Gated out on this platform, or gone. Not a wrapper
                        // after all -- undo the session and try the next one.
                        session_stopped(app);
                        continue;
                    }

                    session_stopped(app);

                    if (!ok && error_out)
                        *error_out = "RunWrapper script failed - see the script console";

                    // The wrapper is the launch. s_process_handle stays NULL,
                    // which poll_running_state already reads as "not running".
                    return ok;
                }
            }

            // On DOS this does not return until the game has exited: see
            // app/process.h. Everything below is the same either way, which
            // is the point of the seam.
            s_process_handle = process_start(path, args, cwd, error_out);

            return s_process_handle != NULL;
        }

        // A run started: record it locally AND tell the server. Both, because
        // the local row is what makes play time work offline, and the server
        // row is what the rest of the ecosystem reads.
        static void session_started(App &app)
        {
            app.games().notify_started(s_game.id);
            s_session_id = app.game_db().begin_play_session(s_game.id, app.user_id());
        }

        // A run ended, however it ended.
        //
        // notify_stopped used to be called only from the Stop button, so a
        // game the user simply quit left the server believing it was still
        // running forever. Routing both the manual and the natural path
        // through here is what fixes that.
        static void session_stopped(App &app)
        {
            if (!s_session_id.empty())
            {
                app.game_db().end_play_session(s_session_id);
                s_session_id.clear();
            }

            if (!s_game.id.empty())
                app.games().notify_stopped(s_game.id);

            // AfterStop is the counterpart to BeforeStart -- copying a save
            // back out, restoring a config the game rewrote. Its failure is
            // reported in the console but changes nothing here: the game has
            // already exited, and there is nothing left to abort.
            if (!s_game.id.empty() && !s_game.install_directory.empty())
            {
                app.script_host().run(lancommander::ScriptType::AfterStop,
                                      script_target(app), NULL);
            }

            // Last, so AfterStop has had its chance to move saves into the
            // paths the manifest describes before they are packed.
            upload_saves(app);
        }

        static void poll_running_state(App &app)
        {
            if (!s_process_handle)
            {
                s_is_running = false;
                s_is_starting = false;
                return;
            }

            if (!process_running(s_process_handle))
            {
                // The game exited on its own — the user quit it rather than
                // pressing Stop. On DOS this is the state on the very first
                // poll, because the launch was synchronous.
                process_close(s_process_handle);
                s_process_handle = NULL;
                s_is_running = false;
                s_is_starting = false;
                s_running_game_id.clear();

                session_stopped(app);
            }
            else
            {
                s_is_running = true;
                s_is_starting = false;
            }
        }

        static void stop_running_game()
        {
            if (s_process_handle)
            {
                process_terminate(s_process_handle);
                process_close(s_process_handle);
                s_process_handle = NULL;
                s_is_running = false;
                s_is_starting = false;
                s_running_game_id.clear();
            }
        }


        // -----------------------------------------------------------------
        // Actions
        // -----------------------------------------------------------------
        //
        // Extracted out of the button handlers they used to live inside, so
        // the menu, the split button and any future right-click menu invoke
        // the same code rather than each growing their own copy.

        static void act_install(App &app)
        {
            const bool multiple_dirs = app.settings().games.install_directories.size() > 1;

            if (!s_addons_loaded)
            {
                s_addons.clear();
                auto addons_result = app.games().get_addons(s_game.id);
                if (addons_result)
                    s_addons = addons_result.value;
                s_addon_selected.assign(s_addons.size(), false);
                s_addons_loaded = true;
            }

            if (multiple_dirs || !s_addons.empty())
            {
                s_install_dir_index = 0;
                s_install_scroll.offset = 0;
                s_modal = ModalType::InstallOptions;
                return;
            }

            std::string install_root;
            if (!app.settings().games.install_directories.empty())
                install_root = app.settings().games.install_directories[0];
            if (install_root.empty())
                install_root = "C:\\Games";

            log_info("Install clicked: %s -> %s", s_game.title.c_str(), install_root.c_str());

            fs_mkdir(install_root);
            const std::string game_dir = install_root + "\\" + s_game.title;
            fs_mkdir(game_dir);

            app.downloads().enqueue(s_game.id, s_game.title, game_dir, !s_game.in_library);
            set_status("Added to download queue", theme().success);
        }

        static void act_launch(App &app, const lancommander::Action &action)
        {
            s_running_game_id = s_game.id;

            std::string err;
            if (launch_action(app, action, &err))
            {
                session_started(app);
                set_status("Running: " + action.name, theme().success);
                s_is_running = true;
                s_is_starting = false;
            }
            else
            {
                set_status(err, theme().error);
                s_is_starting = false;
                s_running_game_id.clear();
            }
        }

        static void act_play(App &app)
        {
            s_modal_actions.clear();
            for (size_t i = 0; i < s_actions.size(); ++i)
                if (s_actions[i].is_primary)
                    s_modal_actions.push_back(&s_actions[i]);

            if (s_modal_actions.size() > 1)
            {
                s_modal = ModalType::ActionSelect;
                return;
            }

            const lancommander::Action *primary = pick_primary_action();
            if (!primary)
            {
                set_status("No actions available", theme().error);
                return;
            }

            s_is_starting = true;
            act_launch(app, *primary);
        }

        static void act_stop(App &app)
        {
            stop_running_game();
            session_stopped(app);
            set_status("Game stopped", theme().text_dim);
        }

        // `which` indexes the NON-PRIMARY subset of s_actions, which is how
        // the menu numbers its dynamic entries.
        static void act_run_secondary(App &app, int which)
        {
            int seen = 0;
            for (size_t i = 0; i < s_actions.size(); ++i)
            {
                if (s_actions[i].is_primary)
                    continue;
                if (seen == which)
                {
                    act_launch(app, s_actions[i]);
                    return;
                }
                ++seen;
            }
        }

        static void act_browse_files(App &app)
        {
            (void)app;

            if (s_game.install_directory.empty())
                return;

            std::string dir = s_game.install_directory;
            normalize_slashes(dir);

#ifdef _WIN32
            ShellExecuteA(NULL, "explore", dir.c_str(), NULL, NULL, SW_SHOWNORMAL);
#else
            // DOS has no file manager to hand the directory to. Showing the
            // path is the useful half of what the button does, rather than
            // having it look broken.
            set_status("Installed at " + dir, theme().text_dim);
#endif
        }

        static void act_uninstall(App &app)
        {
            std::string dir = s_game.install_directory;
            normalize_slashes(dir);

            // Before anything is deleted, while the script and everything it
            // refers to are still on disk. Its failure does not stop the
            // uninstall -- a game whose Uninstall.ps1 is broken must still be
            // removable -- but the console says what went wrong.
            app.script_host().run(lancommander::ScriptType::Uninstall,
                                  script_target(app), NULL);

            // The manifest written during extraction is the only record of
            // what belongs to this game, so without it nothing is deleted
            // rather than guessing at the directory contents.
            const std::string list_path = dir + "\\.lancommander\\" + s_game.id + "\\FileList.txt";

            FILE *fl = fopen(list_path.c_str(), "r");
            if (!fl)
            {
                set_status("No file manifest found", theme().error);
                return;
            }

            char line[1024];
            int deleted = 0;
            while (fgets(line, sizeof(line), fl))
            {
                // Format: "path | CRC32HEX"
                char *sep = strstr(line, " | ");
                size_t len = sep ? (size_t)(sep - line) : strlen(line);
                while (len > 0 && (line[len - 1] == '\n' ||
                       line[len - 1] == '\r' || line[len - 1] == ' '))
                    len--;
                if (len == 0) continue;

                std::string rel(line, len);
                if (rel[rel.size() - 1] == '/' || rel[rel.size() - 1] == '\\')
                    continue;

                for (size_t c = 0; c < rel.size(); ++c)
                    if (rel[c] == '/') rel[c] = '\\';

                if (fs_remove(dir + "\\" + rel))
                    deleted++;
            }
            fclose(fl);

            fs_remove(list_path);
            fs_rmdir(dir + "\\.lancommander\\" + s_game.id);
            fs_rmdir(dir + "\\.lancommander");
            fs_rmdir(dir);

            s_game.install_directory.clear();
            s_actions.clear();
            app.game_db().set_uninstalled(s_game.id);

            // The library list carries install state, so it is now stale.
            app.invalidate_library();

            char msg_buf[64];
            sprintf(msg_buf, "Uninstalled (%d files removed)", deleted);
            set_status(msg_buf, theme().success);
        }

        static void act_add_to_library(App &app)
        {
            app.library().add(s_game.id);
            app.invalidate_library();
            set_status("Added to library", theme().success);
            load_game(app);
        }

        // -----------------------------------------------------------------
        // Gradient helper
        // -----------------------------------------------------------------
        // Fades the hero image into the page background: fully transparent at
        // the top, fully `bg_color` at the bottom.
        static void draw_gradient_bottom(gfx::Surface *buf, int x, int y,
                                         int w, int h, gfx::Color bg_color)
        {
            gfx::fill_rect_gradient_v(buf, gfx::rect(x, y, w, h),
                                      gfx::with_alpha(bg_color, 0),
                                      gfx::with_alpha(bg_color, 255));
        }

        // =================================================================
        // Main draw function
        // =================================================================
        const std::string &screen_game_detail_title()
        {
            return s_game.title;
        }

        void screen_game_detail_draw(App &app, const InputState &raw_input)
        {
            // While the dropdown is open the page beneath it is inert.
            //
            // Without this a click that misses the menu would close it AND
            // activate whatever sat underneath, which is the same defect
            // the global Escape handler used to have with modals.
            const InputState input = (s_menu.open || s_lightbox.open)
                                         ? input_blocked(raw_input)
                                         : raw_input;

            gfx::Surface *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            // Reload if the selected game changed.
            if (app.selected_game() != s_last_game_id)
            {
                load_game(app);
                s_scroll.offset = 0;
                s_modal = ModalType::None;
                s_addons_loaded = false;
            }

            // Check if a download for this game just completed — update
            // install_directory so the UI switches from Install to Play.
            if (s_game.install_directory.empty())
            {
                const std::vector<DownloadItem> &items = app.downloads().items();
                for (size_t i = 0; i < items.size(); ++i)
                {
                    if (items[i].game_id == s_game.id &&
                        items[i].status == DownloadStatus::Complete)
                    {
                        s_game.install_directory = items[i].install_dir;

                        // Record WHICH version was installed, not just that
                        // something was. Without it check_for_update has
                        // nothing to compare against and update detection can
                        // never fire. The manifest is the server's statement
                        // of the build we just downloaded.
                        std::string installed_version;
                        {
                            auto manifest = app.games().get_manifest(s_game.id);
                            if (manifest)
                                installed_version = manifest.value.version;
                        }

                        app.game_db().set_installed(s_game.id, items[i].install_dir,
                                                    installed_version);

                        // The library list carries install state.
                        app.invalidate_library();

                        // Reload actions now that the game is installed.
                        auto actions = app.games().get_actions(s_game.id);
                        if (actions)
                            s_actions = actions.value;
                        break;
                    }
                }
            }

            // Poll running game process.
            if (s_running_game_id == s_game.id)
                poll_running_state(app);
            else
            {
            }

            bool this_game_running = (s_running_game_id == s_game.id && s_is_running);
            bool this_game_starting = (s_running_game_id == s_game.id && s_is_starting);

            int top = chrome_height();
            int th = text_height();

            // =============================================================
            // Compute total page height for scroll bounds
            // =============================================================
            int hero_h = 210;
            int bar_h = 48;
            int cover_col_w = 220;
            int cover_max_w = 180;
            int cover_max_h = 270;
            int cover_overlap = 60;
            int right_x = sw - cover_col_w;
            int left_margin = 24;
            int left_max = right_x - left_margin - 8;

            // Cover
            std::string cover_id = find_cover_id(s_game);
            gfx::Surface *cover = NULL;
            if (!cover_id.empty())
                cover = app.image_cache().get(cover_id, cover_max_w, cover_max_h);

            // Right column height: cover + metadata
            int right_h = 0;
            if (cover) right_h = gfx::surface_height(cover) - cover_overlap + 8;
            right_h += th + 4; // type
            if (s_game.released_year > 0) right_h += th + 4;
            {
                const int meta_w = sw - (right_x + 12) - 12;
                right_h += badge_group_height(meta_w, s_game.genres);
                right_h += badge_group_height(meta_w, s_game.developers);
                right_h += badge_group_height(meta_w, s_game.publishers);
            }

            // Left column height: description
            int left_h = 12;
            if (!s_game.description.empty())
                left_h += draw_text_wrap(NULL, 0, 0, left_max, theme().text,
                                         s_game.description.c_str());

            // The media strip sits under the description and scrolls with it,
            // so the page has to be tall enough to reach it.
            int media_h = 0;
            if (!s_screenshots.empty())
                media_h = 20 + carousel_height(MEDIA_ITEM_H);

            left_h += media_h;

            int body_h = (right_h > left_h ? right_h : left_h) + 20;
            int total_page_h = hero_h + bar_h + body_h;

            // --- Scroll with mouse wheel (entire page) ---
            if (input.mouse.wheel_delta != 0)
            {
                s_scroll.offset -= input.mouse.wheel_delta * 28;
                if (s_scroll.offset < 0) s_scroll.offset = 0;
                int visible_h = sh - top;
                int max_scroll = total_page_h - visible_h;
                if (max_scroll < 0) max_scroll = 0;
                if (s_scroll.offset > max_scroll) s_scroll.offset = max_scroll;
            }

            int sy = -s_scroll.offset; // global scroll offset

            // Clip everything below the chrome bar.
            gfx::push_clip(buf, gfx::rect(0, top, sw, sh - top));

            // =============================================================
            // Hero section
            // =============================================================
            int hero_y = top + sy;

            // Background art, filling the hero band edge to edge and cropped
            // from the centre. This used to fit the image into an sw x sw box
            // and then blit its top-left corner, which cropped a wide hero to
            // its left edge and letterboxed a short one.
            const std::string bg_id = find_media_id(s_game, "Background");
            const gfx::Rect hero_r = gfx::rect(0, hero_y, sw, hero_h);

            if (draw_image_cover(buf, app.image_cache(), hero_r, bg_id))
            {
                draw_gradient_bottom(buf, 0, hero_y + hero_h - 60, sw, 60, theme().bg);
            }
            else
            {
                panel(buf, 0, hero_y, sw, hero_h, theme().panel);
                draw_gradient_bottom(buf, 0, hero_y + hero_h - 40, sw, 40, theme().bg);
            }

            // Logo overlay, bottom-left.
            //
            // Sized as a fraction of the hero rather than to a fixed 200x64
            // box, matching the Avalonia Viewbox: MaxWidth is a third of the
            // band and MaxHeight is half of it. It also uses the CONTAIN fit,
            // which enlarges — the old path fitted WITHIN 200x64 and never
            // upscaled, so a logo authored at 120x30 stayed 120x30 and looked
            // lost in a 210px band, which is why they all read as too small.
            const std::string logo_id = find_media_id(s_game, "Logo");

            const int logo_margin = 24;
            const gfx::Rect logo_box = gfx::rect(logo_margin, hero_y,
                                                 sw / 3, hero_h / 2);

            gfx::Surface *logo_img = logo_id.empty()
                                         ? NULL
                                         : app.image_cache().get(logo_id, logo_box.w,
                                                                 logo_box.h,
                                                                 ImageFit::Contain);

            if (logo_img)
            {
                // Pinned to the bottom-left of the band rather than centred in
                // the box, which is where the Avalonia Viewbox sits it.
                gfx::blit_alpha(buf, logo_img, logo_margin,
                                hero_y + hero_h - 20 - gfx::surface_height(logo_img));
            }
            else
            {
                // GameDetailView draws this at 32 against a base of 16 —
                // the largest thing in either launcher, and the reason the
                // page has a shape at all.
                draw_text(buf, logo_margin,
                          hero_y + hero_h - text_height(FontSize::Display) - 20,
                          theme().text_bright, s_game.title.c_str(),
                          FontSize::Display);
            }

            // --- Cover art (overlaps hero bottom) ---
            if (cover)
            {
                int cx = right_x + (cover_col_w - gfx::surface_width(cover)) / 2;
                int cy = hero_y + hero_h - cover_overlap;
                gfx::blit(buf, cover, cx, cy);
            }

            // --- Back button overlaid on the hero ---
            const char *back_label = (app.library_tab() == LibraryTab::Depot)
                                         ? "Back to Depot" : "Back to Library";
            const int back_gap = 7;
            const int back_w = 12 + ICON_MD + back_gap + text_width(back_label) + 12;
            const int back_h = button_height();
            const int back_x = 8;
            const int back_y = hero_y + 8;

            ButtonState back_btn;
            {
                const gfx::Rect r = gfx::rect(back_x, back_y, back_w, back_h);

                back_btn.hovered = gfx::rect_contains(r, input.mouse.x, input.mouse.y);
                back_btn.clicked = back_btn.hovered && input.mouse.clicked;

                fill_rounded_rect_alpha(buf, r, BUTTON_RADIUS,
                                        gfx::rgba(0, 0, 0, back_btn.hovered ? 190 : 140));

                draw_icon(buf, back_x + 12, back_y + (back_h - ICON_MD) / 2, ICON_MD,
                          theme().text_bright, Icon::ArrowLeft);
                draw_text(buf, back_x + 12 + ICON_MD + back_gap,
                          back_y + (back_h - th) / 2,
                          theme().text_bright, back_label);
            }

            // =============================================================
            // Action bar — directly below hero
            // =============================================================
            //
            // One split button plus one dropdown, replacing the row of ad-hoc
            // buttons that used to grow an entry per feature. The menu
            // contents come from build_game_menu(), a pure function of the
            // game state that is unit-tested.
            const int bar_y = hero_y + hero_h;
            const int bar_pad = 16;
            const int btn_y = bar_y + (bar_h - split_btn_h()) / 2;

            const bool is_installed = !s_game.install_directory.empty();

            GameMenuFlags flags;
            flags.installed = is_installed;
            flags.in_library = s_game.in_library;
            flags.update_available = s_update_available;
            flags.running = this_game_running;
            flags.has_manuals = false;      // manuals arrive with the media work
            flags.offline = false;
            flags.secondary_count = 0;
            for (size_t i = 0; i < s_actions.size(); ++i)
                if (!s_actions[i].is_primary)
                    ++flags.secondary_count;

            const char *primary_label =
                game_primary_label(flags, this_game_starting, false, false);

            gfx::Color primary_bg = theme().button_primary;
            gfx::Color primary_bg_hover = theme().button_primary_hover;
            if (this_game_running)
            {
                primary_bg = theme().error;
                primary_bg_hover = theme().error_hover;
            }

            const SplitButtonResult sb =
                split_button(buf, bar_pad, btn_y, SPLIT_BTN_W, split_btn_h(),
                             primary_label,
                             !this_game_starting, primary_bg, primary_bg_hover, input);

            if (sb.caret_clicked)
            {
                // menu_open() rather than flipping `open` by hand: it also
                // tells context_menu() to ignore this frame's click, which is
                // otherwise still live when the menu draws and slams it shut
                // in the same frame.
                if (s_menu.open)
                    s_menu.open = false;
                else
                    menu_open(s_menu, bar_pad, btn_y + split_btn_h());
            }

            int pending_cmd = 0;
            if (sb.primary_clicked)
                pending_cmd = game_primary_command(flags);

            // --- Stats ---------------------------------------------------
            //
            // Immediately to the right of the action button, as in the
            // Avalonia GameActionBarView, where they sit in the same row and
            // read as belonging to it. Right-aligning them to the content
            // column instead put them the width of the window away from the
            // control they describe.
            {
                // Through App rather than straight to the local database, so
                // these agree with the Recently Played carousel: a game played
                // on another machine has history on the server and none here,
                // and reading only the local table showed "None" / "Never" for
                // it while the library said it was played yesterday.
                const long long secs = app.total_play_seconds(s_game.id);
                const long long played_at = app.last_played_at(s_game.id);

                const std::string play_time = format_play_time((long)secs);
                const std::string last_played =
                    format_last_played((std::time_t)played_at, time(NULL));

                // CompressedSize of the newest archive, matching the
                // Avalonia binding. Blank when the game has no archive —
                // nothing to download — and the column is then skipped
                // rather than drawn with an empty value, as the Avalonia
                // IsVisible converter does.
                const std::string download_size = format_download_size(s_game);

                const char *labels[3] = { "Download Size", "Play Time", "Last Played" };
                const std::string values[3] = { download_size, play_time, last_played };

                int sx = bar_pad + SPLIT_BTN_W + 48;

                // Right edge the stats may reach. Normally the cover art,
                // which overhangs the hero and reaches down into this bar --
                // but the status message is right-aligned into the same row,
                // so while one is up the stats have to stop short of it.
                // Without this, "Added to download queue" printed straight
                // over the Download Size and Play Time columns.
                int stats_right = right_x - 16;
                if (status_visible())
                    stats_right -= text_width(s_status_message.c_str()) + 24;

                for (int i = 0; i < 3; ++i)
                {
                    if (values[i].empty())
                        continue;

                    const int lw = text_width(labels[i]);
                    const int vw = text_width(values[i].c_str());
                    const int w = lw > vw ? lw : vw;

                    if (sx + w > stats_right)
                        break;

                    draw_text(buf, sx, bar_y + 6, theme().text_dim, labels[i],
                              FontSize::Caption);
                    draw_text(buf, sx, bar_y + 6 + th + 2, theme().text,
                              values[i].c_str());

                    sx += w + 32;
                }
            }

            if (status_visible())
            {
                int msg_y = bar_y + (bar_h - th) / 2;
                draw_text_right(buf, right_x - 16, msg_y, s_status_color,
                                s_status_message.c_str());
            }

            // =============================================================
            // Two-column body
            // =============================================================
            int below_bar = bar_y + bar_h;

            // Vertical divider
            gfx::vline(buf, right_x - 1, below_bar, body_h + 1, theme().divider);

            // --- Right column: metadata under cover ---
            int meta_x = right_x + 12;
            int my = below_bar + 12;
            if (cover)
                my = hero_y + hero_h - cover_overlap + gfx::surface_height(cover) + 8;

            const char *type_str = "Main Game";
            switch (s_game.type)
            {
                case lancommander::GameType::Expansion:           type_str = "Expansion"; break;
                case lancommander::GameType::StandaloneExpansion: type_str = "Standalone Expansion"; break;
                case lancommander::GameType::Mod:                 type_str = "Mod"; break;
                case lancommander::GameType::StandaloneMod:       type_str = "Standalone Mod"; break;
                default: break;
            }
            label(buf, meta_x, my, theme().text_dim, type_str);
            my += th + 4;

            if (s_game.released_year > 0)
            {
                char year_buf[32];
                sprintf(year_buf, "Released: %d", s_game.released_year);
                label(buf, meta_x, my, theme().text_dim, year_buf);
                my += th + 4;
            }

            // Genres, developers and publishers as wrapped capsules, the way
            // the Avalonia sidebar draws them with Button.Badge in a WrapPanel.
            // They used to be one plain line each, which made a five-genre
            // game look like a paragraph.
            {
                const int meta_w = sw - meta_x - 12;

                my += draw_badge_group(buf, meta_x, my, meta_w, "Genres",
                                       s_game.genres, input);
                my += draw_badge_group(buf, meta_x, my, meta_w, "Developers",
                                       s_game.developers, input);
                my += draw_badge_group(buf, meta_x, my, meta_w, "Publishers",
                                       s_game.publishers, input);
            }

            // --- Left column: description ---
            int y = below_bar + 12;
            int desc_bottom = y;

            if (!s_game.description.empty())
                desc_bottom = y + draw_text_wrap(buf, left_margin, y, left_max,
                                                 theme().text,
                                                 s_game.description.c_str());

            // =============================================================
            // Media
            // =============================================================
            if (!s_screenshots.empty())
            {
                // Offset so the strip's items line up with the description
                // above them, with the arrows out in the margin.
                const int media_x = left_margin - carousel_gutter();
                const int media_w = (left_max - left_margin) + carousel_gutter() * 2;
                const int item_w = MEDIA_ITEM_W;
                const int item_h = MEDIA_ITEM_H;

                const int my2 = desc_bottom + 20;

                const CarouselResult mr =
                    carousel_begin(buf, media_x, my2, media_w, "Media",
                                   (int)s_screenshots.size(), item_w, item_h, 12,
                                   s_media_carousel, input, false);

                for (int i = mr.first_visible; i <= mr.last_visible; ++i)
                {
                    const gfx::Rect ir = carousel_item_rect(mr, i, item_w, item_h, 12,
                                                            s_media_carousel);

                    gfx::Surface *shot =
                        app.image_cache().get(s_screenshots[i], item_w, item_h);

                    if (shot)
                    {
                        gfx::blit(buf, shot,
                                  ir.x + (item_w - gfx::surface_width(shot)) / 2,
                                  ir.y + (item_h - gfx::surface_height(shot)) / 2);
                    }
                    else
                    {
                        gfx::fill_rect(buf, ir, theme().panel);
                        draw_text_center(buf, ir.x + ir.w / 2, ir.y + ir.h / 2,
                                         theme().text_disabled, "Loading...");
                    }

                    if (mr.hovered_index == i)
                        gfx::draw_rect(buf, ir, theme().primary);
                }

                carousel_end(buf);

                if (mr.clicked_index >= 0)
                {
                    s_lightbox.open = true;
                    s_lightbox.index = mr.clicked_index;
                }

            }

            // Restore clip rect.
            gfx::pop_clip(buf);

            // Scrollbar
            {
                int visible_h = sh - top;
                scrollbar(buf, sw - 14, top, visible_h,
                          total_page_h, visible_h, s_scroll, input);
            }

            // --- Back navigation ---
            if (back_btn.clicked && s_modal == ModalType::None)
                app.switch_screen(Screen::Library);

            // =============================================================
            // Action dropdown
            // =============================================================
            //
            // Drawn after the page so it overlays, but fed the RAW input so it
            // can still be interacted with while everything below is inert.
            {
                GameMenuEntry entries[32];
                const int n = build_game_menu(flags, entries, 32);

                // Dynamic entries carry no label from the builder, which knows
                // counts but not names.
                MenuItemDef defs[32];
                int sec_seen = 0;
                for (int i = 0; i < n; ++i)
                {
                    defs[i].label = entries[i].label;
                    defs[i].command = entries[i].command;
                    defs[i].enabled = entries[i].enabled;

                    if (entries[i].command >= GM_SecondaryBase)
                    {
                        for (size_t a = 0; a < s_actions.size(); ++a)
                        {
                            if (s_actions[a].is_primary)
                                continue;
                            if (sec_seen == entries[i].command - GM_SecondaryBase)
                            {
                                defs[i].label = s_actions[a].name.c_str();
                                break;
                            }
                            ++sec_seen;
                        }
                        sec_seen = 0;
                    }
                }

                const int chosen = context_menu(buf, sw, sh, defs, n, s_menu, raw_input);
                if (chosen != 0)
                    pending_cmd = chosen;
            }

            // =============================================================
            // Lightbox
            // =============================================================
            //
            // Drawn after everything else so it covers the page, and given the
            // RAW input so it stays interactive while the page beneath it is
            // inert.
            if (s_lightbox.open)
            {
                const int count = (int)s_screenshots.size();
                if (s_lightbox.index < 0) s_lightbox.index = 0;
                if (s_lightbox.index >= count) s_lightbox.index = count - 1;

                // Requested at the display size, not the source size: a 4K
                // screenshot decoded at full resolution is a large allocation
                // on the platforms this launcher targets.
                // Between the title bar and the footer, both of which are
                // drawn after this screen and would otherwise cover the
                // controls at the edges.
                const gfx::Rect area = gfx::rect(0, top, sw,
                                                 sh - top - footer_height());

                // Inset so the picture clears the prev/next buttons at the
                // sides and the counter along the bottom rather than sitting
                // underneath them.
                gfx::Surface *full = count > 0
                    ? app.image_cache().get(s_screenshots[s_lightbox.index],
                                            area.w - 110, area.h - 48)
                    : NULL;

                const LightboxAction la =
                    lightbox(buf, area, full, s_lightbox.index, count, raw_input);

                if (la == LightboxAction::Close)
                    s_lightbox.open = false;
                else if (la == LightboxAction::Prev && s_lightbox.index > 0)
                    --s_lightbox.index;
                else if (la == LightboxAction::Next && s_lightbox.index < count - 1)
                    ++s_lightbox.index;
            }

            // An open overlay owns Escape, so App does not navigate away.
            app.set_overlay_active(s_menu.open || s_lightbox.open ||
                                   s_modal != ModalType::None);

            // --- Dispatch -------------------------------------------------
            if (pending_cmd >= GM_SecondaryBase)
            {
                act_run_secondary(app, pending_cmd - GM_SecondaryBase);
            }
            else
            {
                switch (pending_cmd)
                {
                case GM_Install:
                    act_install(app);
                    break;
                case GM_Play:
                case GM_PlayNoUpdate:
                    // Play reads as Stop while the game is running.
                    if (this_game_running)
                        act_stop(app);
                    else if (!this_game_starting)
                        act_play(app);
                    break;
                case GM_Update:
                    // No update path yet; installing over the top is what the
                    // download queue already does.
                    act_install(app);
                    break;
                case GM_BrowseFiles:
                    act_browse_files(app);
                    break;
                case GM_Uninstall:
                    act_uninstall(app);
                    break;
                case GM_AddToLibrary:
                    act_add_to_library(app);
                    break;
                case GM_Modify:
                    s_addons_loaded = false;
                    act_install(app);
                    break;
                default:
                    break;
                }
            }

            // =============================================================
            // Modal dialogs (drawn on top of everything)
            // =============================================================

            if (s_modal == ModalType::ActionSelect)
            {

                int dlg_w = 340;
                int row_h = 30;
                int pad = 16;
                int count = (int)s_modal_actions.size();
                int dlg_h = pad + th + 12 + count * (row_h + 4) + 12 + row_h + pad;

                const gfx::Rect dlg = dialog_begin(buf, sw, sh, dlg_w, dlg_h);
                const int dx = dlg.x;
                const int dy = dlg.y;

                int cy = dy + pad;
                draw_text_center(buf, dx + dlg_w / 2, cy, theme().text_bright, "Choose an action");
                cy += th + 4;
                draw_text_center(buf, dx + dlg_w / 2, cy, theme().text_dim, s_game.title.c_str());
                cy += th + 12;

                int btn_pad = 24;
                int btn_w = dlg_w - btn_pad * 2;

                for (int i = 0; i < count; ++i)
                {
                    ButtonState ab = button(buf, dx + btn_pad, cy, btn_w, row_h,
                                            s_modal_actions[i]->name.c_str(), input);
                    if (ab.clicked)
                    {
                        const lancommander::Action *action = s_modal_actions[i];
                        s_modal = ModalType::None;

                        s_is_starting = true;
                        s_running_game_id = s_game.id;
                        std::string launch_err;
                        if (launch_action(app, *action, &launch_err))
                        {
                            session_started(app);
                            set_status("Running: " + action->name, theme().success);
                            s_is_running = true;
                            s_is_starting = false;
                        }
                        else
                        {
                            set_status(launch_err, theme().error);
                            s_is_starting = false;
                            s_running_game_id.clear();
                        }
                    }
                    cy += row_h + 4;
                }

                cy += 8;
                int cancel_w = 90;
                ButtonState cancel = button(buf, dx + dlg_w - btn_pad - cancel_w, cy,
                                            cancel_w, row_h, "Cancel", input);
                if (cancel.clicked || input.key_pressed(Key::Escape))
                    s_modal = ModalType::None;

                dialog_end(buf);
            }

            if (s_modal == ModalType::InstallOptions)
            {
                const std::vector<std::string> &dirs = app.settings().games.install_directories;
                bool show_dirs = dirs.size() > 1;
                int addon_count = (int)s_addons.size();

                int dlg_w = 420;
                int pad = 16;
                int row_h = 24;
                int dlg_h = pad + th + 12; // title
                if (show_dirs)
                    dlg_h += th + 4 + row_h + 12; // dir label + dropdown + gap
                if (addon_count > 0)
                {
                    dlg_h += th + 8; // "Add-ons" header
                    int visible = addon_count > 8 ? 8 : addon_count;
                    dlg_h += visible * (row_h + 2) + 8;
                }
                dlg_h += 12 + 28 + pad; // gap + buttons + bottom pad

                const gfx::Rect dlg = dialog_begin(buf, sw, sh, dlg_w, dlg_h);
                const int dx = dlg.x;
                const int dy = dlg.y;

                int cx = dx + pad;
                int content_w = dlg_w - pad * 2;
                int cy = dy + pad;

                // Title
                char title_buf[128];
                sprintf(title_buf, "Install %s", s_game.title.c_str());
                draw_text_center(buf, dx + dlg_w / 2, cy, theme().text_bright, title_buf);
                cy += th + 12;

                // Install directory selector
                if (show_dirs)
                {
                    label(buf, cx, cy, theme().text_dim, "Install Directory");
                    cy += th + 4;

                    // Draw as prev/next selector since we don't have a real dropdown
                    int sel_w = content_w - 60;
                    int arrow_w = 26;

                    // Left arrow
                    ButtonState left_btn = icon_button(buf, cx, cy, arrow_w, row_h,
                                                       Icon::CaretLeft, NULL, input);
                    if (left_btn.clicked && s_install_dir_index > 0)
                        s_install_dir_index--;

                    // Current directory text
                    gfx::fill_rect(buf, gfx::rect(cx + arrow_w + 2, cy, sel_w, row_h),
                                   theme().input_bg);
                    gfx::draw_rect(buf, gfx::rect(cx + arrow_w + 2, cy, sel_w, row_h),
                                   theme().input_border);
                    {
                        const char *dir_text = "";
                        if (s_install_dir_index >= 0 && s_install_dir_index < (int)dirs.size())
                            dir_text = dirs[s_install_dir_index].c_str();
                        gfx::push_clip(buf, gfx::rect(cx + arrow_w + 6, cy,
                                                      sel_w - 10, row_h));
                        draw_text(buf, cx + arrow_w + 6,
                                  cy + (row_h - th) / 2, theme().text, dir_text);
                        gfx::pop_clip(buf);
                    }

                    // Right arrow
                    ButtonState right_btn = icon_button(buf, cx + arrow_w + 2 + sel_w + 2, cy,
                                                        arrow_w, row_h,
                                                        Icon::CaretRight, NULL, input);
                    if (right_btn.clicked && s_install_dir_index < (int)dirs.size() - 1)
                        s_install_dir_index++;

                    cy += row_h + 12;
                }

                // Addons
                if (addon_count > 0)
                {
                    draw_text(buf, cx, cy, theme().text_bright, "Optional Add-ons");

                    // Select All / None buttons
                    {
                        int all_w = text_width("All") + 12;
                        int none_w = text_width("None") + 12;
                        int bx = cx + content_w - all_w - 4 - none_w;
                        ButtonState all_btn = button(buf, bx, cy - 2, all_w, th + 4, "All", input);
                        if (all_btn.clicked)
                            for (int i = 0; i < addon_count; ++i)
                                s_addon_selected[i] = 1;
                        bx += all_w + 4;
                        ButtonState none_btn = button(buf, bx, cy - 2, none_w, th + 4, "None", input);
                        if (none_btn.clicked)
                            for (int i = 0; i < addon_count; ++i)
                                s_addon_selected[i] = 0;
                    }

                    cy += th + 8;

                    int visible = addon_count > 8 ? 8 : addon_count;
                    int list_h = visible * (row_h + 2);

                    gfx::push_clip(buf, gfx::rect(cx, cy, content_w, list_h));

                    int ay = cy - s_install_scroll.offset;
                    for (int i = 0; i < addon_count; ++i)
                    {
                        if (ay + row_h >= cy && ay < cy + list_h)
                        {
                            const char *type_str = "";
                            switch (s_addons[i].type)
                            {
                            case lancommander::GameType::Expansion:
                            case lancommander::GameType::StandaloneExpansion:
                                type_str = "Expansion"; break;
                            case lancommander::GameType::Mod:
                            case lancommander::GameType::StandaloneMod:
                                type_str = "Mod"; break;
                            default: break;
                            }

                            bool sel = (s_addon_selected[i] != 0);
                            checkbox(buf, cx, ay, s_addons[i].title.c_str(), sel, input);
                            s_addon_selected[i] = sel ? 1 : 0;

                            if (type_str[0])
                                draw_text_right(buf, cx + content_w, ay + (row_h - th) / 2,
                                                theme().text_dim, type_str);
                        }
                        ay += row_h + 2;
                    }
                    gfx::pop_clip(buf);

                    // Scroll for long addon lists
                    if (addon_count > visible)
                    {
                        int total_addon_h = addon_count * (row_h + 2);
                        scrollbar(buf, cx + content_w + 2, cy, list_h,
                                  total_addon_h, list_h, s_install_scroll, input);
                    }

                    cy += list_h + 8;
                }

                // Buttons
                cy += 4;
                int btn_w = 90;
                int btn_h2 = button_height();
                int btn_gap = 8;

                ButtonState cancel = button(buf, dx + dlg_w - pad - btn_w, cy,
                                            btn_w, btn_h2, "Cancel", input);

                ButtonState install = button(buf, dx + dlg_w - pad - btn_w - btn_gap - btn_w, cy,
                                              btn_w, btn_h2, "Install", input,
                                              ButtonStyle::Primary);

                if (install.clicked)
                {
                    // Get selected install directory
                    std::string install_root;
                    if (show_dirs && s_install_dir_index >= 0 &&
                        s_install_dir_index < (int)dirs.size())
                        install_root = dirs[s_install_dir_index];
                    else if (!dirs.empty())
                        install_root = dirs[0];
                    if (install_root.empty())
                        install_root = "C:\\Games";

                    log_info("Install (dialog): %s -> %s", s_game.title.c_str(), install_root.c_str());

                    fs_mkdir(install_root);
                    std::string game_dir = install_root + "\\" + s_game.title;
                    fs_mkdir(game_dir);

                    bool needs_lib_add = !s_game.in_library;
                    app.downloads().enqueue(s_game.id, s_game.title, game_dir, needs_lib_add);

                    // Enqueue selected addons
                    for (int i = 0; i < addon_count; ++i)
                    {
                        if (s_addon_selected[i])
                        {
                            std::string addon_dir = game_dir;
                            log_info("Addon selected: %s", s_addons[i].title.c_str());
                            app.downloads().enqueue(s_addons[i].id, s_addons[i].title,
                                                    addon_dir, false);
                        }
                    }

                    set_status("Added to download queue", theme().success);
                    s_modal = ModalType::None;
                }

                if (cancel.clicked || input.key_pressed(Key::Escape))
                    s_modal = ModalType::None;

                dialog_end(buf);
            }

        }

    } // namespace ui
} // namespace launcher
