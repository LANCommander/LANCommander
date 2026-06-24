#include "ui/screen_settings.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "app/app.h"
#include "app/logger.h"

#include <allegro.h>
#include <cstdio>

namespace launcher
{
    namespace ui
    {

        static const int MAX_INSTALL_DIRS = 16;

        // Persistent form state — initialised from Settings on first draw.
        static bool s_initialized = false;
        static std::string s_server_address;
        static std::vector<std::string> s_install_dirs;
        static bool s_offline_mode = false;
        static std::string s_status_message;
        static int  s_status_color = 0;
        // Focus: 0 = server address, 1..N = install dir fields
        static int  s_focus = 0;
        static int  s_scroll_y = 0;

        void screen_settings_draw(App &app, const InputState &input)
        {
            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            int top = chrome_height();
            int bottom = sh - footer_height();
            int th = text_height();
            int pad = 16;

            // --- Header bar ---
            int header_h = 40;
            int header_y = top;
            panel(buf, 0, header_y, sw, header_h, theme().surface);
            hline(buf, 0, header_y + header_h - 1, sw - 1, theme().divider);

            // Back button
            int back_w = 60;
            int back_h = 22;
            int back_x = pad;
            int back_y = header_y + (header_h - back_h) / 2;
            ButtonState back_btn = button(buf, back_x, back_y, back_w, back_h, "< Back", input);

            // Title
            draw_text(buf, back_x + back_w + 12, header_y + (header_h - th) / 2,
                      theme().text_bright, "Settings");

            // --- Initialize form from settings on first visit ---
            if (!s_initialized)
            {
                s_server_address = app.settings().authentication.server_address;
                s_install_dirs   = app.settings().games.install_directories;
                if (s_install_dirs.empty())
                    s_install_dirs.push_back("C:\\Games");
                s_offline_mode = app.settings().authentication.offline_mode;
                s_status_message.clear();
                s_focus = 0;
                s_scroll_y = 0;
                s_initialized = true;
            }

            int dir_count = (int)s_install_dirs.size();
            int total_focus_fields = 1 + dir_count; // server + N dirs

            // --- Content area ---
            int content_y = header_y + header_h;
            int content_h = bottom - content_y;

            // Form layout
            int form_w = 460;
            if (form_w > sw - pad * 2) form_w = sw - pad * 2;
            int form_x = (sw - form_w) / 2;
            int field_h = 22;
            int section_gap = 24;
            int field_gap = 10;
            int dir_row_h = field_h + 4; // field + gap between dir rows
            int remove_btn_w = 22;

            // Calculate total content height for scrolling
            int total_h = pad
                        + th + 8                           // "Connection" header
                        + th + 4 + field_h                 // server address
                        + field_gap
                        + 22                               // offline mode checkbox
                        + section_gap
                        + th + 8                           // "Install Directories" header
                        + dir_count * dir_row_h            // directory fields
                        + field_gap + 22                   // add button
                        + section_gap
                        + 28                               // save button
                        + 8 + th                           // status message
                        + pad;

            // Scroll
            if (input.mouse.y >= content_y && input.mouse.y < bottom &&
                input.mouse.wheel_delta != 0)
            {
                s_scroll_y -= input.mouse.wheel_delta * 28;
                if (s_scroll_y < 0) s_scroll_y = 0;
                int max_scroll = total_h - content_h;
                if (max_scroll < 0) max_scroll = 0;
                if (s_scroll_y > max_scroll) s_scroll_y = max_scroll;
            }

            set_clip_rect(buf, 0, content_y, sw - 1, bottom - 1);

            int y = content_y + pad - s_scroll_y;

            // =============================================================
            // Connection section
            // =============================================================
            draw_text(buf, form_x, y, theme().text_bright, "Connection");
            y += th + 8;

            // --- Server Address ---
            label(buf, form_x, y, theme().text_dim, "Server Address");
            y += th + 4;

            TextInputState addr_state = text_input(buf, form_x, y, form_w, field_h,
                                                    s_server_address, 256,
                                                    s_focus == 0, input);

            if (input.mouse.clicked && input.mouse.x >= form_x &&
                input.mouse.x < form_x + form_w &&
                input.mouse.y >= y && input.mouse.y < y + field_h)
                s_focus = 0;

            if (addr_state.submitted && dir_count > 0)
                s_focus = 1;

            y += field_h + field_gap;

            // --- Offline Mode ---
            {
                int cb_size = 16;
                int cb_y = y + (22 - cb_size) / 2;

                rect(buf, form_x, cb_y, form_x + cb_size - 1, cb_y + cb_size - 1,
                     theme().input_border);

                if (s_offline_mode)
                    rectfill(buf, form_x + 3, cb_y + 3,
                             form_x + cb_size - 4, cb_y + cb_size - 4,
                             theme().primary);

                draw_text(buf, form_x + cb_size + 8, y + (22 - th) / 2,
                          theme().text, "Offline Mode");

                bool cb_hovered = (input.mouse.x >= form_x &&
                                   input.mouse.x < form_x + cb_size + 8 + text_width("Offline Mode") &&
                                   input.mouse.y >= y && input.mouse.y < y + 22);
                if (cb_hovered && input.mouse.clicked)
                    s_offline_mode = !s_offline_mode;

                y += 22;
            }

            y += section_gap;

            // =============================================================
            // Install Directories section
            // =============================================================
            draw_text(buf, form_x, y, theme().text_bright, "Install Directories");
            y += th + 8;

            int remove_idx = -1;

            for (int i = 0; i < dir_count; ++i)
            {
                int focus_idx = 1 + i;
                int input_w = form_w;

                // Show remove button if more than one directory
                if (dir_count > 1)
                    input_w = form_w - remove_btn_w - 4;

                text_input(buf, form_x, y, input_w, field_h,
                           s_install_dirs[i], 256,
                           s_focus == focus_idx, input);

                if (input.mouse.clicked && input.mouse.x >= form_x &&
                    input.mouse.x < form_x + input_w &&
                    input.mouse.y >= y && input.mouse.y < y + field_h)
                    s_focus = focus_idx;

                // Remove button
                if (dir_count > 1)
                {
                    int rx = form_x + input_w + 4;
                    ButtonState rm = button(buf, rx, y, remove_btn_w, field_h, "X", input);
                    if (rm.clicked)
                        remove_idx = i;
                }

                y += dir_row_h;
            }

            // Handle removal after the loop (avoids invalidating indices)
            if (remove_idx >= 0)
            {
                s_install_dirs.erase(s_install_dirs.begin() + remove_idx);
                dir_count = (int)s_install_dirs.size();
                if (s_focus > dir_count)
                    s_focus = dir_count;
                total_focus_fields = 1 + dir_count;
            }

            // --- Add Directory button ---
            if (dir_count < MAX_INSTALL_DIRS)
            {
                y += field_gap;
                int add_w = text_width("+ Add Directory") + 20;
                ButtonState add_btn = button(buf, form_x, y, add_w, field_h, "+ Add Directory", input);
                if (add_btn.clicked)
                {
                    s_install_dirs.push_back("");
                    s_focus = 1 + (int)s_install_dirs.size() - 1;
                    total_focus_fields = 1 + (int)s_install_dirs.size();
                }
                y += field_h;
            }

            y += section_gap;

            // =============================================================
            // Save button
            // =============================================================
            int btn_w = 100;
            int save_btn_h = 28;
            int btn_x = form_x + (form_w - btn_w) / 2;

            ButtonState save_btn = button(buf, btn_x, y, btn_w, save_btn_h, "Save", input);

            if (save_btn.clicked)
            {
                app.settings().authentication.server_address = s_server_address;
                app.settings().authentication.offline_mode   = s_offline_mode;

                // Filter out empty entries
                app.settings().games.install_directories.clear();
                for (size_t i = 0; i < s_install_dirs.size(); ++i)
                {
                    if (!s_install_dirs[i].empty())
                        app.settings().games.install_directories.push_back(s_install_dirs[i]);
                }

                if (!s_server_address.empty())
                    app.connection().set_server_address(s_server_address);

                if (s_offline_mode)
                    app.connection().enable_offline_mode();

                s_status_message = "Settings saved";
                s_status_color = theme().success;
                log_info("Settings saved: server=%s dirs=%d offline=%s",
                         s_server_address.c_str(),
                         (int)app.settings().games.install_directories.size(),
                         s_offline_mode ? "true" : "false");
            }

            y += save_btn_h + 8;

            // --- Status message ---
            if (!s_status_message.empty())
                draw_text_center(buf, sw / 2, y, s_status_color,
                                 s_status_message.c_str());

            set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

            // Scrollbar
            scrollbar(buf, sw - 14, content_y, content_h,
                      total_h, content_h, s_scroll_y, input);

            // --- Tab between fields ---
            if (input.key_pressed(KEY_TAB))
                s_focus = (s_focus + 1) % total_focus_fields;

            // --- Back navigation ---
            if (back_btn.clicked)
            {
                s_initialized = false;
                app.switch_screen(Screen::Library);
            }
        }

    } // namespace ui
} // namespace launcher
