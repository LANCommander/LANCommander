#include "ui/screen_login.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "ui/auth_background.h"
#include "ui/screen_server_select.h"
#include "ui/image_decoder.h"
#include "app/app.h"
#include "app/logger.h"

#include "gfx/gfx.h"

#include <cstdlib>
#include <ctime>

namespace launcher
{
    namespace ui
    {

        // Persistent state for the login screen.
        static std::string s_server_address;
        static std::string s_username;
        static std::string s_password;
        static std::string s_error_message;
        static int s_focus = 1; // 1=username, 2=password

        // One caret/selection per field. Sharing one would move every caret
        // together the moment focus changed.
        static TextEditState s_user_edit;
        static TextEditState s_pass_edit;
        static bool s_initialized = false;
        static bool s_connecting = false;

        void screen_login_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            // Initialize fields from saved settings on first draw.
            if (!s_initialized)
            {
                s_server_address = app.settings().authentication.server_address;
                s_username = app.settings().launcher.username;
                s_password.clear();
                s_error_message.clear();
                s_initialized = true;
            }

            auth_background_draw(buf, sw, sh);

            // --- Layout (center below chrome) ---
            int top = chrome_height();
            int panel_w = 320;
            int panel_h = 280;
            int px = (sw - panel_w) / 2;
            int py = top + (sh - top - panel_h) / 2;

            panel(buf, px, py, panel_w, panel_h, theme().panel);

            int cx = px + panel_w / 2;
            int y = py + 16;

            draw_text_center(buf, cx, y, theme().text_bright, "LANCommander");
            y += text_height() + 4;
            draw_text_center(buf, cx, y, theme().text_dim, "Connect to a server");
            y += text_height() + 16;

            // --- Server address ---
            int field_x = px + 24;
            int field_w = panel_w - 48;
            int field_h = 22;

            // The server is chosen on the previous screen; here it is just
            // stated, with a way back. Avalonia hides this behind a hover
            // cross-fade, which is invisible to anyone not using a mouse, so
            // it is a plain button instead.
            label(buf, field_x, y, theme().text_dim, "Server");
            y += text_height() + 4;

            {
                const int change_w = 64;
                const int change_h = 20;
                const int change_x = field_x + field_w - change_w;

                draw_text(buf, field_x, y + 2, theme().text,
                          s_server_address.empty() ? "(none)" : s_server_address.c_str());

                if (button(buf, change_x, y, change_w, change_h, "Change", input).clicked)
                {
                    s_initialized = false;
                    screen_server_select_reset();
                    app.switch_screen(Screen::ServerSelect);
                    return;
                }
            }

            y += field_h + 10;

            // --- Username ---
            label(buf, field_x, y, theme().text_dim, "Username");
            y += text_height() + 4;

            TextInputState user_state = text_input(buf, field_x, y, field_w, field_h, s_username, 64, s_focus == 1, input, s_user_edit);

            if (input.mouse.clicked && input.mouse.x >= field_x && input.mouse.x < field_x + field_w && input.mouse.y >= y && input.mouse.y < y + field_h)
                s_focus = 1;

            if (user_state.submitted)
                s_focus = 2;

            y += field_h + 10;

            // --- Password ---
            label(buf, field_x, y, theme().text_dim, "Password");
            y += text_height() + 4;

            TextInputState pass_state = text_input(buf, field_x, y, field_w, field_h, s_password, 128, s_focus == 2, input, s_pass_edit, true);

            if (input.mouse.clicked && input.mouse.x >= field_x && input.mouse.x < field_x + field_w && input.mouse.y >= y && input.mouse.y < y + field_h)
                s_focus = 2;

            y += field_h + 14;

            // --- Login button ---
            int btn_w = 100;
            int btn_h = 28;
            int btn_x = px + (panel_w - btn_w) / 2;

            ButtonState btn = button(buf, btn_x, y, btn_w, btn_h, s_connecting ? "Connecting..." : "Login", input);

            bool do_login = btn.clicked || pass_state.submitted;

            if (do_login && !s_connecting)
            {
                s_error_message.clear();
                s_connecting = true;

                // Set server address.
                if (!s_server_address.empty())
                    app.connection().set_server_address(s_server_address);

                // Attempt login.
                log_info("Login attempt: %s@%s", s_username.c_str(), s_server_address.c_str());
                auto token = app.auth().login(s_username, s_password);

                if (token)
                {
                    log_info("Login successful");
                    app.connection().set_access_token(token.value.access_token);
                    app.connection().connect();

                    // Save to settings.
                    app.settings().authentication.server_address = s_server_address;
                    app.settings().authentication.token.access_token = token.value.access_token;
                    app.settings().authentication.token.refresh_token = token.value.refresh_token;
                    app.settings().launcher.username = s_username;

                    // Get user alias.
                    lancommander::ProfileClient profile(app.http());
                    auto alias = profile.get_alias();

                    if (alias)
                        app.set_user_alias(alias.value);

                    s_connecting = false;
                    s_initialized = false;
                    app.switch_screen(Screen::Library);

                    return;
                }
                else
                {
                    s_error_message = token.error;
                    s_connecting = false;
                    log_error("Login failed: %s", token.error.c_str());
                }
            }

            y += btn_h + 8;

            // --- Error message ---
            if (!s_error_message.empty())
                draw_text_center(buf, cx, y, theme().error, s_error_message.c_str());

            // Tab between fields
            if (input.key_pressed(Key::Tab))
                s_focus = (s_focus == 1) ? 2 : 1;

        }
    } // namespace ui
} // namespace launcher
