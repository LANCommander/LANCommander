// window_chrome.cpp — the launcher's custom title bar and footer.
//
// Pure drawing and hit-testing against the backbuffer: no windows.h, no
// backend headers. Everything that needs a real window (going frameless,
// resize edges, dragging, minimising) lives behind chrome_platform.h.

#include "ui/window_chrome.h"
#include "ui/chrome_platform.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "app/app.h"
#include "gfx/gfx.h"

#include <cstdio>
#include <cstring>
#include <vector>

#ifdef _WIN32
// Last Win32 holdout in this file: the title-bar icon is still pulled out of
// the EXE's ICON resource with GetIconInfo/GetDIBits. It goes away when the
// image stack moves to stb and the icon ships as a PNG asset.
#include <windows.h>
#endif

namespace launcher
{
    namespace ui
    {

        static const int CHROME_H = 32;
        static const int FOOTER_H = 40;
        static const int BTN_W = 36;

        static const ChromeMetrics g_metrics = {
            CHROME_H,
            FOOTER_H,
            6,   // resize_border — grab width of the window edges
            640, // min_w
            480  // min_h
        };

        const ChromeMetrics &chrome_metrics() { return g_metrics; }

        int chrome_height() { return CHROME_H; }
        int footer_height() { return FOOTER_H; }


        // ---------------------------------------------------------------
        // Title bar icon — loaded once from the EXE resource.
        // ---------------------------------------------------------------
        static gfx::Surface *s_icon_bmp = NULL;
        static bool s_icon_loaded = false;
        static const int ICON_SIZE = 20; // display size in the title bar

        static void ensure_icon_loaded()
        {
            if (s_icon_loaded) return;
            s_icon_loaded = true;

#ifdef _WIN32
            // Load the icon resource embedded via launcher.rc.
            HICON hIcon = (HICON)LoadImageA(
                GetModuleHandle(NULL), "IDI_ICON1", IMAGE_ICON,
                ICON_SIZE, ICON_SIZE, LR_DEFAULTCOLOR);
            if (!hIcon) return;

            // Extract pixels via GetIconInfo + GetDIBits.
            ICONINFO ii;
            if (!GetIconInfo(hIcon, &ii))
            {
                DestroyIcon(hIcon);
                return;
            }

            HDC hdc = GetDC(NULL);

            BITMAPINFOHEADER bih;
            memset(&bih, 0, sizeof(bih));
            bih.biSize = sizeof(bih);
            bih.biWidth = ICON_SIZE;
            bih.biHeight = -ICON_SIZE; // top-down
            bih.biPlanes = 1;
            bih.biBitCount = 32;
            bih.biCompression = BI_RGB;

            unsigned char *pixels = new unsigned char[ICON_SIZE * ICON_SIZE * 4];

            GetDIBits(hdc, ii.hbmColor, 0, ICON_SIZE,
                      pixels, (BITMAPINFO *)&bih, DIB_RGB_COLORS);

            ReleaseDC(NULL, hdc);

            // Swizzle BGRA -> RGBA, pre-multiplying against black so a plain
            // opaque blit looks right. (Phase 1 replaces this whole GDI icon
            // path with a stb-decoded PNG and a real alpha blit.)
            {
                std::vector<unsigned char> rgba((size_t)ICON_SIZE * ICON_SIZE * 4);
                for (int i = 0; i < ICON_SIZE * ICON_SIZE; ++i)
                {
                    const int b = pixels[i * 4 + 0];
                    const int g = pixels[i * 4 + 1];
                    const int r = pixels[i * 4 + 2];
                    const int a = pixels[i * 4 + 3];

                    rgba[i * 4 + 0] = (unsigned char)(r * a / 255);
                    rgba[i * 4 + 1] = (unsigned char)(g * a / 255);
                    rgba[i * 4 + 2] = (unsigned char)(b * a / 255);
                    rgba[i * 4 + 3] = 255;
                }
                s_icon_bmp = gfx::surface_from_rgba(&rgba[0], ICON_SIZE, ICON_SIZE);
            }

            delete[] pixels;
            DeleteObject(ii.hbmColor);
            DeleteObject(ii.hbmMask);
            DestroyIcon(hIcon);
#endif
        }

        // Dropdown menu state.
        static bool s_user_dropdown_open = false;

        // Dropdown menu item IDs.
        enum class UserMenuItem
        {
            None,
            Settings,
            GoOffline,
            Logout
        };

        bool window_chrome_draw(App &app, const InputState &input)
        {

            gfx::Surface *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();
            bool close_clicked = false;

            // --- Semi-transparent black overlay (50% opacity) ---
            gfx::fill_rect_alpha(buf, gfx::rect(0, 0, sw, CHROME_H),
                                 gfx::rgba(0, 0, 0, 128));

            // --- Icon + Title ---
            ensure_icon_loaded();
            int title_x = 10;
            if (s_icon_bmp)
            {
                int icon_y = (CHROME_H - ICON_SIZE) / 2;
                gfx::blit(buf, s_icon_bmp, title_x, icon_y);
                title_x += ICON_SIZE + 6;
            }
            draw_text(buf, title_x, (CHROME_H - text_height()) / 2,
                      theme().text_bright, "LANCommander");

            // --- Close button (right edge) ---
            int close_x = sw - BTN_W;
            {
                bool hovered = (input.mouse.x >= close_x && input.mouse.x < close_x + BTN_W &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);
                if (hovered)
                    panel(buf, close_x, 0, BTN_W, CHROME_H,
                          gfx::rgb(0xC4, 0x2B, 0x1C)); // Windows red close

                int cx = close_x + BTN_W / 2;
                gfx::Color color = hovered ? theme().text_bright : theme().text_dim;
                draw_text_center(buf, cx, (CHROME_H - text_height()) / 2, color, "X");

                if (hovered && input.mouse.pressed)
                    close_clicked = true;
            }

            // --- Minimize button ---
            int min_x = close_x - BTN_W;
            {
                bool hovered = (input.mouse.x >= min_x && input.mouse.x < min_x + BTN_W &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);
                if (hovered)
                    gfx::fill_rect_alpha(buf, gfx::rect(min_x, 0, BTN_W, CHROME_H),
                                         gfx::rgba(255, 255, 255, 25));

                int cx = min_x + BTN_W / 2;
                gfx::Color color = hovered ? theme().text_bright : theme().text_dim;
                draw_text_center(buf, cx, (CHROME_H - text_height()) / 2, color, "_");

                if (hovered && input.mouse.pressed)
                {
                    chrome_platform_minimize();
                }
            }

            // --- User dropdown button (left of minimize) ---
            int user_btn_x = 0;
            int user_btn_w = 0;
            int user_btn_right = min_x;

            std::string alias_str = app.user_alias();
            if (!alias_str.empty())
            {
                int pad = 16;
                int th = text_height();
                int alias_w = text_width(alias_str.c_str());
                user_btn_w = pad + alias_w + pad;
                user_btn_x = user_btn_right - user_btn_w;

                bool hovered = (input.mouse.x >= user_btn_x && input.mouse.x < user_btn_right &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);

                // Background — always primary blue, darker when active.
                gfx::Color bg = (s_user_dropdown_open || hovered) ? theme().primary_active : theme().primary;
                panel(buf, user_btn_x, 0, user_btn_w, CHROME_H, bg);

                int text_y = (CHROME_H - th) / 2;
                draw_text(buf, user_btn_x + pad, text_y, theme().text_bright, alias_str.c_str());

                // Toggle on click.
                if (hovered && input.mouse.clicked)
                    s_user_dropdown_open = !s_user_dropdown_open;
            }

            // --- User dropdown menu ---
            UserMenuItem menu_action = UserMenuItem::None;

            if (s_user_dropdown_open && user_btn_w > 0)
            {
                int menu_item_h = CHROME_H;
                int menu_w = user_btn_w;
                if (menu_w < 160) menu_w = 160;
                int menu_x = user_btn_right - menu_w;
                int menu_y = CHROME_H;

                const char *items[] = { "Settings", "Go Offline", "Logout" };
                UserMenuItem ids[] = {
                    UserMenuItem::Settings,
                    UserMenuItem::GoOffline,
                    UserMenuItem::Logout
                };
                int item_count = 3;
                int menu_h = item_count * menu_item_h;

                // Clamp menu to screen.
                if (menu_y + menu_h > sh)
                    menu_h = sh - menu_y;

                // Menu background.
                panel(buf, menu_x, menu_y, menu_w, menu_h, theme().surface);
                gfx::draw_rect(buf, gfx::rect(menu_x, menu_y, menu_w, menu_h),
                               theme().divider);

                // Check if the offline label should say "Go Online" instead.
                if (app.settings().authentication.offline_mode)
                    items[1] = "Go Online";

                bool any_item_hovered = false;
                for (int i = 0; i < item_count; ++i)
                {
                    int iy = menu_y + i * menu_item_h;
                    if (iy + menu_item_h > menu_y + menu_h)
                        break;

                    bool item_hovered = (input.mouse.x >= menu_x &&
                                         input.mouse.x < menu_x + menu_w &&
                                         input.mouse.y >= iy &&
                                         input.mouse.y < iy + menu_item_h);

                    if (item_hovered)
                    {
                        any_item_hovered = true;
                        panel(buf, menu_x + 1, iy, menu_w - 2, menu_item_h,
                              theme().panel_hover);
                    }

                    // Separator above "Logout".
                    if (i == item_count - 1)
                        gfx::hline(buf, menu_x + 1, iy, menu_w - 2, theme().divider);

                    int text_y = iy + (menu_item_h - text_height()) / 2;
                    gfx::Color color = item_hovered ? theme().text_bright : theme().text;

                    // Logout in red.
                    if (ids[i] == UserMenuItem::Logout && !item_hovered)
                        color = theme().error;

                    draw_text(buf, menu_x + 12, text_y, color, items[i]);

                    if (item_hovered && input.mouse.clicked)
                        menu_action = ids[i];
                }

                // Close menu when clicking outside.
                bool in_menu = (input.mouse.x >= menu_x && input.mouse.x < menu_x + menu_w &&
                                input.mouse.y >= menu_y && input.mouse.y < menu_y + menu_h);
                bool in_button = (input.mouse.x >= user_btn_x && input.mouse.x < user_btn_right &&
                                  input.mouse.y >= 0 && input.mouse.y < CHROME_H);

                if (input.mouse.clicked && !in_menu && !in_button)
                    s_user_dropdown_open = false;
            }

            // Handle menu actions.
            if (menu_action != UserMenuItem::None)
            {
                s_user_dropdown_open = false;

                switch (menu_action)
                {
                case UserMenuItem::Settings:
                    app.switch_screen(Screen::Settings);
                    break;
                case UserMenuItem::GoOffline:
                {
                    bool offline = !app.settings().authentication.offline_mode;
                    app.settings().authentication.offline_mode = offline;
                    if (offline)
                        app.connection().enable_offline_mode();
                    break;
                }
                case UserMenuItem::Logout:
                    app.settings().authentication.token.access_token.clear();
                    app.settings().authentication.token.refresh_token.clear();
                    app.set_user_alias("");
                    app.switch_screen(Screen::Login);
                    break;
                default:
                    break;
                }
            }

            // --- Drag handling ---
            // Publish the draggable span so the platform hit test agrees with
            // what we just drew. Dragging is suppressed while the dropdown is
            // open so the click that should dismiss it isn't swallowed.
            const int drag_right = (user_btn_w > 0) ? user_btn_x : min_x;
            chrome_platform_frame(drag_right, !s_user_dropdown_open);

            const bool in_drag_area = (input.mouse.x >= 0 && input.mouse.x < drag_right &&
                                       input.mouse.y >= 0 && input.mouse.y < CHROME_H);

            if (in_drag_area && input.mouse.pressed && !s_user_dropdown_open)
                chrome_platform_begin_drag();

            return close_clicked;
        }

        // ---------------------------------------------------------------
        // Footer bar: Depot/Library toggle + download status
        // ---------------------------------------------------------------

        static void format_bytes(unsigned long bytes, char *buf, int buf_sz)
        {
            if (bytes >= 1024UL * 1024UL * 1024UL)
                sprintf(buf, "%.1f GB", bytes / (1024.0 * 1024.0 * 1024.0));
            else if (bytes >= 1024UL * 1024UL)
                sprintf(buf, "%.1f MB", bytes / (1024.0 * 1024.0));
            else if (bytes >= 1024UL)
                sprintf(buf, "%.0f KB", bytes / 1024.0);
            else
                sprintf(buf, "%lu B", bytes);
        }

        void window_footer_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            int fy = sh - FOOTER_H;
            int th = text_height();

            // ==== Footer bar ====
            panel(buf, 0, fy, sw, FOOTER_H, theme().footer);
            gfx::hline(buf, 0, fy, sw, theme().divider);

            int pad = 12;
            int btn_h = 24;
            int btn_y = fy + (FOOTER_H - btn_h) / 2;

            // --- Left: Depot / Library toggle (only show the inactive button) ---
            {
                bool depot_active = (app.library_tab() == LibraryTab::Depot);

                const char *label = depot_active ? "Library" : "Depot";
                int bw = text_width(label) + 20;
                int bx = pad;

                bool hovered = (input.mouse.x >= bx && input.mouse.x < bx + bw &&
                                input.mouse.y >= btn_y && input.mouse.y < btn_y + btn_h);

                if (hovered)
                    gfx::fill_rect(buf, gfx::rect(bx, btn_y, bw, btn_h), theme().panel_hover);

                draw_text_center(buf, bx + bw / 2, btn_y + (btn_h - th) / 2,
                                 theme().text_dim, label);

                if (hovered && input.mouse.clicked)
                {
                    app.set_library_tab(depot_active ? LibraryTab::Library : LibraryTab::Depot);
                    if (app.current_screen() != Screen::Library)
                        app.switch_screen(Screen::Library);
                }
            }

            // --- Center: Download progress or Downloads button ---
            {
                const DownloadItem *cur = app.downloads().current_item();
                bool on_downloads_screen = (app.current_screen() == Screen::Downloads);

                if (cur && (cur->status == DownloadStatus::Downloading ||
                            cur->status == DownloadStatus::Extracting))
                {
                    // Show active download: title + progress bar + percentage.
                    // Clicking navigates to the Downloads screen.
                    int cx = sw / 2;
                    int info_w = 300;
                    int info_x = cx - info_w / 2;

                    bool area_hovered = (input.mouse.x >= info_x && input.mouse.x < info_x + info_w &&
                                         input.mouse.y >= fy && input.mouse.y < fy + FOOTER_H);

                    // Title (left)
                    gfx::push_clip(buf, gfx::rect(info_x, fy, info_w - 80, FOOTER_H));
                    draw_text(buf, info_x, btn_y + 1, theme().text, cur->title.c_str());
                    gfx::pop_clip(buf);

                    // Percentage (right)
                    char pct[16];
                    sprintf(pct, "%d%%", (int)(cur->progress * 100));
                    draw_text_right(buf, info_x + info_w, btn_y + 1,
                                    theme().text_dim, pct);

                    // Progress bar
                    int bar_x = info_x;
                    int bar_w = info_w;
                    int bar_y2 = btn_y + btn_h + 1;
                    gfx::fill_rect(buf, gfx::rect(bar_x, bar_y2, bar_w, 3), theme().panel);
                    int fill = (int)(cur->progress * bar_w);
                    if (fill > 0)
                        gfx::fill_rect(buf, gfx::rect(bar_x, bar_y2, fill, 3), theme().primary);

                    if (area_hovered && input.mouse.clicked && !on_downloads_screen)
                        app.switch_screen(Screen::Downloads);
                }
                else
                {
                    // "Downloads" button — navigates to the Downloads screen.
                    int pending = app.downloads().pending_count();
                    char dl_label[32];
                    if (pending > 0)
                        sprintf(dl_label, "Downloads (%d)", pending);
                    else
                        sprintf(dl_label, "Downloads");

                    int dl_w = text_width(dl_label) + 20;
                    int dl_x = sw / 2 - dl_w / 2;

                    bool dl_hovered = (input.mouse.x >= dl_x && input.mouse.x < dl_x + dl_w &&
                                       input.mouse.y >= btn_y && input.mouse.y < btn_y + btn_h);

                    if (on_downloads_screen)
                        gfx::fill_rect(buf, gfx::rect(dl_x, btn_y, dl_w, btn_h), theme().primary);
                    else if (dl_hovered)
                        gfx::fill_rect(buf, gfx::rect(dl_x, btn_y, dl_w, btn_h), theme().panel_hover);

                    draw_text_center(buf, dl_x + dl_w / 2, btn_y + (btn_h - th) / 2,
                                     (on_downloads_screen || dl_hovered)
                                         ? theme().text_bright : theme().text_disabled,
                                     dl_label);

                    if (dl_hovered && input.mouse.clicked && !on_downloads_screen)
                        app.switch_screen(Screen::Downloads);
                }
            }

        }

    } // namespace ui
} // namespace launcher
