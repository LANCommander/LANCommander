#include "ui/screen_login.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "ui/image_decoder.h"
#include "app/app.h"
#include "app/logger.h"

#include <allegro.h>
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
        static int s_focus = 0; // 0=server, 1=username, 2=password
        static bool s_initialized = false;
        static bool s_connecting = false;

        // Background image state.
        static BITMAP *s_bg_bitmap = NULL;
        static bool s_bg_loaded = false;

        static const char *BG_RESOURCE_NAMES[] = {
            "BG_AOE2",
            "BG_BFME2",
            "BG_CSS",
            "BG_NS2",
            "BG_SOLDAT2",
            "BG_UT2004",
        };
        static const int BG_COUNT = sizeof(BG_RESOURCE_NAMES) / sizeof(BG_RESOURCE_NAMES[0]);

        static BITMAP *load_login_background(int max_w, int max_h)
        {
            static bool seeded = false;
            if (!seeded) { srand((unsigned)time(NULL)); seeded = true; }

            int idx = rand() % BG_COUNT;

            DecodedImage img = {};
            if (!decode_image_resource(BG_RESOURCE_NAMES[idx], max_w, max_h, &img))
                return NULL;

            BITMAP *bmp = create_bitmap_ex(32, img.width, img.height);
            if (bmp)
            {
                for (int y = 0; y < img.height; y++)
                {
                    unsigned char *row = img.pixels + y * img.width * 4;
                    for (int x = 0; x < img.width; x++)
                    {
                        int r = row[x * 4 + 0];
                        int g = row[x * 4 + 1];
                        int b = row[x * 4 + 2];
                        putpixel(bmp, x, y, makecol(r, g, b));
                    }
                }
            }
            free_decoded_image(&img);
            return bmp;
        }

        void screen_login_draw(App &app, const InputState &input)
        {
            BITMAP *buf = app.backbuffer();
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

            // --- Background image ---
            if (!s_bg_loaded)
            {
                s_bg_bitmap = load_login_background(sw, sh);
                s_bg_loaded = true;
            }

            if (s_bg_bitmap)
            {
                // Center-crop blit (UniformToFill)
                int src_x = 0, src_y = 0;
                int src_w = s_bg_bitmap->w;
                int src_h = s_bg_bitmap->h;

                if (src_w * sh > src_h * sw)
                {
                    // Image is wider than screen aspect — crop sides
                    int scaled_w = src_h * sw / sh;
                    src_x = (src_w - scaled_w) / 2;
                    src_w = scaled_w;
                }
                else
                {
                    // Image is taller than screen aspect — crop top/bottom
                    int scaled_h = src_w * sh / sw;
                    src_y = (src_h - scaled_h) / 2;
                    src_h = scaled_h;
                }

                stretch_blit(s_bg_bitmap, buf, src_x, src_y, src_w, src_h, 0, 0, sw, sh);

                // Darken overlay so the login panel is readable
                set_trans_blender(0, 0, 0, 140);
                drawing_mode(DRAW_MODE_TRANS, NULL, 0, 0);
                rectfill(buf, 0, 0, sw - 1, sh - 1, makecol(0, 0, 0));
                drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);
            }

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

            label(buf, field_x, y, theme().text_dim, "Server Address");
            y += text_height() + 4;

            TextInputState addr_state = text_input(buf, field_x, y, field_w, field_h, s_server_address, 256, s_focus == 0, input);

            if (input.mouse.clicked && input.mouse.x >= field_x && input.mouse.x < field_x + field_w &&
                input.mouse.y >= y && input.mouse.y < y + field_h)
                s_focus = 0;

            if (addr_state.submitted)
                s_focus = 1;

            y += field_h + 10;

            // --- Username ---
            label(buf, field_x, y, theme().text_dim, "Username");
            y += text_height() + 4;

            TextInputState user_state = text_input(buf, field_x, y, field_w, field_h, s_username, 64, s_focus == 1, input);

            if (input.mouse.clicked && input.mouse.x >= field_x && input.mouse.x < field_x + field_w && input.mouse.y >= y && input.mouse.y < y + field_h)
                s_focus = 1;

            if (user_state.submitted)
                s_focus = 2;

            y += field_h + 10;

            // --- Password ---
            label(buf, field_x, y, theme().text_dim, "Password");
            y += text_height() + 4;

            TextInputState pass_state = text_input(buf, field_x, y, field_w, field_h, s_password, 128, s_focus == 2, input, true);

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
            if (input.key_pressed(KEY_TAB))
                s_focus = (s_focus + 1) % 3;

        }
    } // namespace ui
} // namespace launcher
