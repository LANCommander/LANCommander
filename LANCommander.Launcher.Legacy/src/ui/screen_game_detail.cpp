#include "ui/screen_game_detail.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "ui/image_cache.h"
#include "app/app.h"
#include "app/game_database.h"
#include "app/logger.h"

#include <allegro.h>
#include <cstdio>
#include <cstring>

#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
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
        static int s_status_color = 0;

        // Scroll state
        static int s_scroll_y = 0;

        // Running game tracking
        static std::string s_running_game_id;
        static void *s_process_handle = NULL; // HANDLE
        static bool s_is_running = false;
        static bool s_is_starting = false;

        // -----------------------------------------------------------------
        // Modal dialog state
        // -----------------------------------------------------------------
        enum class ModalType { None, ActionSelect, InstallOptions };
        static ModalType s_modal = ModalType::None;

        // Action selection dialog
        static std::vector<const lancommander::Action *> s_modal_actions;

        // Install options dialog
        static std::vector<lancommander::Game> s_addons;
        // Using char instead of bool because vector<bool> is a
        // special-cased bitfield that doesn't support references.
        static std::vector<char> s_addon_selected;
        static int s_install_dir_index = 0;
        static bool s_addons_loaded = false;
        static int s_install_scroll_y = 0;

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
                s_status_message = result.error;
                s_status_color = theme().error;
            }

            auto actions = app.games().get_actions(app.selected_game());

            if (actions)
                s_actions = actions.value;
            else
                s_actions.clear();

            s_last_game_id = app.selected_game();
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

        static bool launch_action(App &app, const lancommander::Action &action,
                                  std::string *error_out)
        {
#ifdef ALLEGRO_WINDOWS
            std::string install_dir = s_game.install_directory;
            normalize_slashes(install_dir);
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

            SHELLEXECUTEINFOA info;
            memset(&info, 0, sizeof(info));
            info.cbSize = sizeof(info);
            info.fMask = SEE_MASK_FLAG_NO_UI | SEE_MASK_NOCLOSEPROCESS;
            info.lpVerb = "open";
            info.lpFile = path.c_str();
            info.lpParameters = args.empty() ? NULL : args.c_str();
            info.lpDirectory = cwd.empty() ? NULL : cwd.c_str();
            info.nShow = SW_SHOWNORMAL;

            if (!ShellExecuteExA(&info))
            {
                if (error_out)
                {
                    char buf[128];
                    sprintf(buf, "Launch failed (error %lu)", GetLastError());
                    *error_out = buf;
                }
                return false;
            }

            s_process_handle = info.hProcess;
            return true;
#else
            if (error_out)
                *error_out = "Launch not supported on this platform";
            return false;
#endif
        }

        static void poll_running_state()
        {
#ifdef ALLEGRO_WINDOWS
            if (!s_process_handle)
            {
                s_is_running = false;
                s_is_starting = false;
                return;
            }

            DWORD result = WaitForSingleObject((HANDLE)s_process_handle, 0);
            if (result == WAIT_OBJECT_0)
            {
                // Process exited.
                CloseHandle((HANDLE)s_process_handle);
                s_process_handle = NULL;
                s_is_running = false;
                s_is_starting = false;
                s_running_game_id.clear();
            }
            else
            {
                s_is_running = true;
                s_is_starting = false;
            }
#endif
        }

        static void stop_running_game()
        {
#ifdef ALLEGRO_WINDOWS
            if (s_process_handle)
            {
                TerminateProcess((HANDLE)s_process_handle, 0);
                CloseHandle((HANDLE)s_process_handle);
                s_process_handle = NULL;
                s_is_running = false;
                s_is_starting = false;
                s_running_game_id.clear();
            }
#endif
        }

        // -----------------------------------------------------------------
        // Gradient helper
        // -----------------------------------------------------------------
        static void draw_gradient_bottom(BITMAP *buf, int x, int y,
                                         int w, int h, int bg_color)
        {
            int br = getr(bg_color);
            int bgc = getg(bg_color);
            int bb = getb(bg_color);

            for (int row = 0; row < h; row++)
            {
                int alpha = 255 * row / (h > 1 ? h - 1 : 1);
                for (int col = 0; col < w; col++)
                {
                    int px = getpixel(buf, x + col, y + row);
                    int pr = getr(px);
                    int pg = getg(px);
                    int pb = getb(px);
                    int r = pr + (br - pr) * alpha / 255;
                    int g = pg + (bgc - pg) * alpha / 255;
                    int b = pb + (bb - pb) * alpha / 255;
                    putpixel(buf, x + col, y + row, makecol(r, g, b));
                }
            }
        }

        // -----------------------------------------------------------------
        // Large primary button (different styling from the standard button)
        // -----------------------------------------------------------------
        static ButtonState button_large(BITMAP *bmp, int x, int y, int w, int h,
                                        const char *label_text, const InputState &input,
                                        int bg_color, int bg_hover_color)
        {
            ButtonState state;
            state.hovered = (input.mouse.x >= x && input.mouse.x < x + w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            state.clicked = state.hovered && input.mouse.clicked;

            int bg = state.hovered ? bg_hover_color : bg_color;
            rectfill(bmp, x, y, x + w - 1, y + h - 1, bg);

            int tx = x + (w - text_width(label_text)) / 2;
            int ty = y + (h - text_height()) / 2;
            draw_text(bmp, tx, ty, theme().text_bright, label_text);

            return state;
        }

        // Secondary (outline-style) button
        static ButtonState button_secondary(BITMAP *bmp, int x, int y, int w, int h,
                                            const char *label_text, const InputState &input)
        {
            ButtonState state;
            state.hovered = (input.mouse.x >= x && input.mouse.x < x + w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            state.clicked = state.hovered && input.mouse.clicked;

            int bg = state.hovered ? theme().panel_hover : theme().panel;
            rectfill(bmp, x, y, x + w - 1, y + h - 1, bg);
            rect(bmp, x, y, x + w - 1, y + h - 1, theme().divider);

            int tx = x + (w - text_width(label_text)) / 2;
            int ty = y + (h - text_height()) / 2;
            draw_text(bmp, tx, ty, theme().text, label_text);

            return state;
        }

        // =================================================================
        // Main draw function
        // =================================================================
        void screen_game_detail_draw(App &app, const InputState &input)
        {
            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            // Reload if the selected game changed.
            if (app.selected_game() != s_last_game_id)
            {
                load_game(app);
                s_scroll_y = 0;
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

                        // Persist to local database.
                        app.game_db().set_installed(s_game.id, items[i].install_dir);

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
                poll_running_state();
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
            BITMAP *cover = NULL;
            if (!cover_id.empty())
                cover = app.image_cache().get(cover_id, cover_max_w, cover_max_h);

            // Right column height: cover + metadata
            int right_h = 0;
            if (cover) right_h = cover->h - cover_overlap + 8;
            right_h += th + 4; // type
            if (s_game.released_year > 0) right_h += th + 4;
            if (!s_game.genres.empty())
                right_h += th + 2 + (int)s_game.genres.size() * (th + 1) + 4;
            if (!s_game.developers.empty())
                right_h += th + 2 + (int)s_game.developers.size() * (th + 1) + 4;
            if (!s_game.publishers.empty())
                right_h += th + 2 + (int)s_game.publishers.size() * (th + 1) + 4;

            // Left column height: description
            int left_h = 12;
            if (!s_game.description.empty())
                left_h += draw_text_wrap(NULL, 0, 0, left_max, 0,
                                         s_game.description.c_str());

            int body_h = (right_h > left_h ? right_h : left_h) + 20;
            int total_page_h = hero_h + bar_h + body_h;

            // --- Scroll with mouse wheel (entire page) ---
            if (input.mouse.wheel_delta != 0)
            {
                s_scroll_y -= input.mouse.wheel_delta * 28;
                if (s_scroll_y < 0) s_scroll_y = 0;
                int visible_h = sh - top;
                int max_scroll = total_page_h - visible_h;
                if (max_scroll < 0) max_scroll = 0;
                if (s_scroll_y > max_scroll) s_scroll_y = max_scroll;
            }

            int sy = -s_scroll_y; // global scroll offset

            // Clip everything below the chrome bar.
            set_clip_rect(buf, 0, top, sw - 1, sh - 1);

            // =============================================================
            // Hero section
            // =============================================================
            int hero_y = top + sy;

            std::string bg_id = find_media_id(s_game, "Background");
            BITMAP *bg_img = NULL;
            if (!bg_id.empty())
                bg_img = app.image_cache().get(bg_id, sw, sw);

            if (bg_img)
            {
                int blit_w = bg_img->w < sw ? bg_img->w : sw;
                int blit_h = bg_img->h < hero_h ? bg_img->h : hero_h;
                int dst_x = (sw - blit_w) / 2;
                int dst_y = hero_y;
                if (bg_img->h < hero_h)
                    dst_y = hero_y + (hero_h - bg_img->h) / 2;
                blit(bg_img, buf, 0, 0, dst_x, dst_y, blit_w, blit_h);
                draw_gradient_bottom(buf, 0, hero_y + hero_h - 60, sw, 60, theme().bg);
            }
            else
            {
                panel(buf, 0, hero_y, sw, hero_h, theme().panel);
                draw_gradient_bottom(buf, 0, hero_y + hero_h - 40, sw, 40, theme().bg);
            }

            // Logo overlay
            std::string logo_id = find_media_id(s_game, "Logo");
            BITMAP *logo_img = NULL;
            if (!logo_id.empty())
                logo_img = app.image_cache().get(logo_id, 200, 64);

            if (logo_img)
            {
                set_alpha_blender();
                draw_trans_sprite(buf, logo_img, 24, hero_y + hero_h - logo_img->h - 16);
            }
            else
            {
                draw_text(buf, 24, hero_y + hero_h - th - 20,
                          theme().text_bright, s_game.title.c_str());
            }

            // --- Cover art (overlaps hero bottom) ---
            if (cover)
            {
                int cx = right_x + (cover_col_w - cover->w) / 2;
                int cy = hero_y + hero_h - cover_overlap;
                blit(cover, buf, 0, 0, cx, cy, cover->w, cover->h);
            }

            // --- Back button overlaid on the hero ---
            const char *back_label = (app.library_tab() == LibraryTab::Depot)
                                         ? "Back to Depot" : "Back to Library";
            int back_w = text_width(back_label) + 20;
            int back_h = 22;
            int back_x = 8;
            int back_y = hero_y + 8;

            ButtonState back_btn;
            {
                back_btn.hovered = (input.mouse.x >= back_x && input.mouse.x < back_x + back_w &&
                                    input.mouse.y >= back_y && input.mouse.y < back_y + back_h);
                back_btn.clicked = back_btn.hovered && input.mouse.clicked;

                drawing_mode(DRAW_MODE_TRANS, NULL, 0, 0);
                set_trans_blender(0, 0, 0, back_btn.hovered ? 180 : 140);
                rectfill(buf, back_x, back_y, back_x + back_w - 1, back_y + back_h - 1,
                         makecol(0, 0, 0));
                drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);

                int tx = back_x + (back_w - text_width(back_label)) / 2;
                int ty = back_y + (back_h - th) / 2;
                draw_text(buf, tx, ty, theme().text_bright, back_label);
            }

            // =============================================================
            // Action bar — directly below hero
            // =============================================================
            int bar_y = hero_y + hero_h;
            int bar_pad = 16;
            int btn_x = bar_pad;
            int btn_y = bar_y + (bar_h - 30) / 2;

            bool is_installed = !s_game.install_directory.empty();

            if (!is_installed)
            {
                int btn_w = 140;
                ButtonState install_btn = button_large(buf, btn_x, btn_y, btn_w, 30,
                                                       "Install", input,
                                                       theme().primary, theme().primary_hover);
                if (install_btn.clicked)
                {
                    bool multiple_dirs = app.settings().games.install_directories.size() > 1;

                    // Fetch addons if not loaded yet
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
                        // Show install options dialog
                        s_install_dir_index = 0;
                        s_install_scroll_y = 0;
                        s_modal = ModalType::InstallOptions;
                    }
                    else
                    {
                        // Direct install — single directory, no addons
                        std::string install_root;
                        if (!app.settings().games.install_directories.empty())
                            install_root = app.settings().games.install_directories[0];
                        if (install_root.empty())
                            install_root = "C:\\Games";

                        log_info("Install clicked: %s -> %s", s_game.title.c_str(), install_root.c_str());

                        CreateDirectoryA(install_root.c_str(), NULL);
                        std::string game_dir = install_root + "\\" + s_game.title;
                        CreateDirectoryA(game_dir.c_str(), NULL);

                        bool needs_lib_add = !s_game.in_library;
                        app.downloads().enqueue(s_game.id, s_game.title, game_dir, needs_lib_add);
                        s_status_message = "Added to download queue";
                        s_status_color = theme().success;
                    }
                }
                btn_x += btn_w + 8;

                if (!s_game.in_library)
                {
                    int lib_w = 120;
                    ButtonState lib_btn = button_secondary(buf, btn_x, btn_y, lib_w, 30,
                                                           "Add to Library", input);
                    if (lib_btn.clicked)
                    {
                        app.library().add(s_game.id);
                        s_status_message = "Added to library";
                        s_status_color = theme().success;
                        load_game(app);
                    }
                    btn_x += lib_w + 8;
                }
            }
            else
            {
                int play_w = 140;
                const char *play_label;
                int play_bg, play_bg_hover;

                if (this_game_starting)
                {
                    play_label = "Starting...";
                    play_bg = theme().primary;
                    play_bg_hover = theme().primary;
                }
                else if (this_game_running)
                {
                    play_label = "Stop";
                    play_bg = theme().error;
                    play_bg_hover = makecol(200, 60, 60);
                }
                else
                {
                    play_label = "Play";
                    play_bg = theme().primary;
                    play_bg_hover = theme().primary_hover;
                }

                ButtonState play_btn = button_large(buf, btn_x, btn_y, play_w, 30,
                                                    play_label, input, play_bg, play_bg_hover);

                if (play_btn.clicked && !this_game_starting)
                {
                    if (this_game_running)
                    {
                        stop_running_game();
                        app.games().notify_stopped(s_game.id);
                        s_status_message = "Game stopped";
                        s_status_color = theme().text_dim;
                    }
                    else
                    {
                        // Count primary actions
                        s_modal_actions.clear();
                        for (size_t i = 0; i < s_actions.size(); ++i)
                            if (s_actions[i].is_primary)
                                s_modal_actions.push_back(&s_actions[i]);

                        if (s_modal_actions.size() > 1)
                        {
                            // Multiple primary actions — show selection dialog
                            s_modal = ModalType::ActionSelect;
                        }
                        else
                        {
                            // Single or zero primary actions — launch directly
                            const lancommander::Action *primary = pick_primary_action();
                            if (primary)
                            {
                                s_is_starting = true;
                                s_running_game_id = s_game.id;
                                std::string launch_err;
                                if (launch_action(app, *primary, &launch_err))
                                {
                                    app.games().notify_started(s_game.id);
                                    s_status_message = "Running: " + primary->name;
                                    s_status_color = theme().success;
                                    s_is_running = true;
                                    s_is_starting = false;
                                }
                                else
                                {
                                    s_status_message = launch_err;
                                    s_status_color = theme().error;
                                    s_is_starting = false;
                                    s_running_game_id.clear();
                                }
                            }
                            else
                            {
                                s_status_message = "No actions available";
                                s_status_color = theme().error;
                            }
                        }
                    }
                }
                btn_x += play_w + 8;

                for (size_t i = 0; i < s_actions.size() && i < 4; ++i)
                {
                    if (s_actions[i].is_primary) continue;
                    const char *name = s_actions[i].name.c_str();
                    int sec_w = text_width(name) + 24;
                    if (sec_w < 80) sec_w = 80;
                    if (btn_x + sec_w > sw - bar_pad) break;
                    ButtonState sec_btn = button_secondary(buf, btn_x, btn_y, sec_w, 30, name, input);
                    if (sec_btn.clicked && !this_game_running)
                    {
                        std::string err;
                        s_running_game_id = s_game.id;
                        if (launch_action(app, s_actions[i], &err))
                        {
                            app.games().notify_started(s_game.id);
                            s_status_message = "Running: " + s_actions[i].name;
                            s_status_color = theme().success;
                            s_is_running = true;
                        }
                        else
                        {
                            s_status_message = err;
                            s_status_color = theme().error;
                            s_running_game_id.clear();
                        }
                    }
                    btn_x += sec_w + 8;
                }

                if (!this_game_running && !this_game_starting)
                {
                    int unsw = text_width("Uninstall") + 24;
                    int uns_x = sw - bar_pad - unsw;
                    if (uns_x > btn_x + 8)
                    {
                        ButtonState uns_btn = button_secondary(buf, uns_x, btn_y, unsw, 30,
                                                               "Uninstall", input);
                        if (uns_btn.clicked)
                        {
#ifdef ALLEGRO_WINDOWS
                            std::string dir = s_game.install_directory;
                            normalize_slashes(dir);

                            // Read the file manifest written during extraction.
                            std::string list_path = dir + "\\.lancommander\\" +
                                                    s_game.id + "\\FileList.txt";

                            FILE *fl = fopen(list_path.c_str(), "r");
                            if (fl)
                            {
                                char line[1024];
                                int deleted = 0;
                                while (fgets(line, sizeof(line), fl))
                                {
                                    // Format: "path | CRC32HEX"
                                    char *sep = strstr(line, " | ");
                                    size_t len = sep ? (size_t)(sep - line) : strlen(line);
                                    // Trim trailing whitespace/newline.
                                    while (len > 0 && (line[len - 1] == '\n' ||
                                           line[len - 1] == '\r' || line[len - 1] == ' '))
                                        len--;
                                    if (len == 0) continue;

                                    std::string rel(line, len);
                                    // Skip directory entries (trailing slash).
                                    if (rel[rel.size() - 1] == '/' ||
                                        rel[rel.size() - 1] == '\\')
                                        continue;

                                    for (size_t c = 0; c < rel.size(); ++c)
                                        if (rel[c] == '/') rel[c] = '\\';

                                    std::string full = dir + "\\" + rel;
                                    if (DeleteFileA(full.c_str()))
                                        deleted++;
                                }
                                fclose(fl);

                                // Remove empty directories bottom-up.
                                // Walk dir tree and remove empties (best-effort).
                                // Start by removing the metadata directory.
                                DeleteFileA(list_path.c_str());
                                std::string meta_game = dir + "\\.lancommander\\" + s_game.id;
                                RemoveDirectoryA(meta_game.c_str());
                                // Try removing .lancommander if empty.
                                std::string meta_root = dir + "\\.lancommander";
                                RemoveDirectoryA(meta_root.c_str());
                                // Try removing the install dir itself if empty.
                                RemoveDirectoryA(dir.c_str());

                                s_game.install_directory.clear();
                                s_actions.clear();

                                // Remove from local database.
                                app.game_db().set_uninstalled(s_game.id);

                                char msg_buf[64];
                                sprintf(msg_buf, "Uninstalled (%d files removed)", deleted);
                                s_status_message = msg_buf;
                                s_status_color = theme().success;
                            }
                            else
                            {
                                s_status_message = "No file manifest found";
                                s_status_color = theme().error;
                            }
#endif
                        }
                    }
                }
            }

            if (!s_status_message.empty())
            {
                int msg_y = bar_y + (bar_h - th) / 2;
                draw_text_right(buf, sw - bar_pad, msg_y, s_status_color,
                                s_status_message.c_str());
            }

            // =============================================================
            // Two-column body
            // =============================================================
            int below_bar = bar_y + bar_h;

            // Vertical divider
            vline(buf, right_x - 1, below_bar, below_bar + body_h, theme().divider);

            // --- Right column: metadata under cover ---
            int meta_x = right_x + 12;
            int my = below_bar + 12;
            if (cover)
                my = hero_y + hero_h - cover_overlap + cover->h + 8;

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

            if (!s_game.genres.empty())
            {
                label(buf, meta_x, my, theme().text_dim, "Genres");
                my += th + 2;
                for (size_t i = 0; i < s_game.genres.size(); ++i)
                {
                    label(buf, meta_x, my, theme().text, s_game.genres[i].c_str());
                    my += th + 1;
                }
                my += 4;
            }

            if (!s_game.developers.empty())
            {
                label(buf, meta_x, my, theme().text_dim, "Developer");
                my += th + 2;
                for (size_t i = 0; i < s_game.developers.size(); ++i)
                {
                    label(buf, meta_x, my, theme().text, s_game.developers[i].c_str());
                    my += th + 1;
                }
                my += 4;
            }

            if (!s_game.publishers.empty())
            {
                label(buf, meta_x, my, theme().text_dim, "Publisher");
                my += th + 2;
                for (size_t i = 0; i < s_game.publishers.size(); ++i)
                {
                    label(buf, meta_x, my, theme().text, s_game.publishers[i].c_str());
                    my += th + 1;
                }
                my += 4;
            }

            // --- Left column: description ---
            int y = below_bar + 12;

            if (!s_game.description.empty())
                draw_text_wrap(buf, left_margin, y, left_max, theme().text,
                               s_game.description.c_str());

            // Restore clip rect.
            set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

            // Scrollbar
            {
                int visible_h = sh - top;
                scrollbar(buf, sw - 14, top, visible_h,
                          total_page_h, visible_h, s_scroll_y, input);
            }

            // --- Back navigation ---
            if (back_btn.clicked && s_modal == ModalType::None)
                app.switch_screen(Screen::Library);

            // =============================================================
            // Modal dialogs (drawn on top of everything)
            // =============================================================

            if (s_modal == ModalType::ActionSelect)
            {
                modal_backdrop(buf, sw, sh);

                int dlg_w = 340;
                int row_h = 30;
                int pad = 16;
                int count = (int)s_modal_actions.size();
                int dlg_h = pad + th + 12 + count * (row_h + 4) + 12 + row_h + pad;
                int dx = (sw - dlg_w) / 2;
                int dy = (sh - dlg_h) / 2;

                panel(buf, dx, dy, dlg_w, dlg_h, theme().panel);
                rect(buf, dx, dy, dx + dlg_w - 1, dy + dlg_h - 1, theme().divider);

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
                            app.games().notify_started(s_game.id);
                            s_status_message = "Running: " + action->name;
                            s_status_color = theme().success;
                            s_is_running = true;
                            s_is_starting = false;
                        }
                        else
                        {
                            s_status_message = launch_err;
                            s_status_color = theme().error;
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
                if (cancel.clicked || input.key_pressed(KEY_ESC))
                    s_modal = ModalType::None;
            }

            if (s_modal == ModalType::InstallOptions)
            {
                modal_backdrop(buf, sw, sh);

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

                int dx = (sw - dlg_w) / 2;
                int dy = (sh - dlg_h) / 2;

                panel(buf, dx, dy, dlg_w, dlg_h, theme().panel);
                rect(buf, dx, dy, dx + dlg_w - 1, dy + dlg_h - 1, theme().divider);

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
                    ButtonState left_btn = button(buf, cx, cy, arrow_w, row_h, "<", input);
                    if (left_btn.clicked && s_install_dir_index > 0)
                        s_install_dir_index--;

                    // Current directory text
                    rectfill(buf, cx + arrow_w + 2, cy,
                             cx + arrow_w + 2 + sel_w - 1, cy + row_h - 1,
                             theme().input_bg);
                    rect(buf, cx + arrow_w + 2, cy,
                         cx + arrow_w + 2 + sel_w - 1, cy + row_h - 1,
                         theme().input_border);
                    {
                        const char *dir_text = "";
                        if (s_install_dir_index >= 0 && s_install_dir_index < (int)dirs.size())
                            dir_text = dirs[s_install_dir_index].c_str();
                        set_clip_rect(buf, cx + arrow_w + 6, cy,
                                      cx + arrow_w + sel_w - 4, cy + row_h - 1);
                        draw_text(buf, cx + arrow_w + 6,
                                  cy + (row_h - th) / 2, theme().text, dir_text);
                        set_clip_rect(buf, 0, 0, sw - 1, sh - 1);
                    }

                    // Right arrow
                    ButtonState right_btn = button(buf, cx + arrow_w + 2 + sel_w + 2, cy,
                                                    arrow_w, row_h, ">", input);
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

                    set_clip_rect(buf, cx, cy, cx + content_w - 1, cy + list_h - 1);

                    int ay = cy - s_install_scroll_y;
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
                    set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

                    // Scroll for long addon lists
                    if (addon_count > visible)
                    {
                        int total_addon_h = addon_count * (row_h + 2);
                        scrollbar(buf, cx + content_w + 2, cy, list_h,
                                  total_addon_h, list_h, s_install_scroll_y, input);
                    }

                    cy += list_h + 8;
                }

                // Buttons
                cy += 4;
                int btn_w = 90;
                int btn_h2 = 28;
                int btn_gap = 8;

                ButtonState cancel = button(buf, dx + dlg_w - pad - btn_w, cy,
                                            btn_w, btn_h2, "Cancel", input);

                ButtonState install = button(buf, dx + dlg_w - pad - btn_w - btn_gap - btn_w, cy,
                                              btn_w, btn_h2, "Install", input);

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

                    CreateDirectoryA(install_root.c_str(), NULL);
                    std::string game_dir = install_root + "\\" + s_game.title;
                    CreateDirectoryA(game_dir.c_str(), NULL);

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

                    s_status_message = "Added to download queue";
                    s_status_color = theme().success;
                    s_modal = ModalType::None;
                }

                if (cancel.clicked || input.key_pressed(KEY_ESC))
                    s_modal = ModalType::None;
            }
        }

    } // namespace ui
} // namespace launcher
