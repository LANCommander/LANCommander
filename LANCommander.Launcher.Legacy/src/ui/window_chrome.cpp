// window_chrome.cpp — the launcher's custom title bar and footer.
//
// Pure drawing and hit-testing against the backbuffer: no windows.h, no
// backend headers. Everything that needs a real window (going frameless,
// resize edges, dragging, minimising) lives behind chrome_platform.h.

#include "ui/window_chrome.h"
#include "ui/chrome_platform.h"
#include "ui/screen_game_detail.h"
#include "ui/icons.h"
#include "ui/image_decoder.h"
#include "app/paths.h"
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

        // 52 in the Avalonia shell footer, times the 0.8 the two launchers
        // sit at.
        static const int FOOTER_H = 42;

        // Caption button cell. Avalonia's are 46 x 40 — the Windows 11 metric
        // — and this title bar is 32 tall, so the same aspect is 37 x 32.
        static const int BTN_W = 37;

        // The caption glyphs, which are the one place the two launchers do
        // not draw the same artwork: Avalonia spells them with text (an em
        // dash, a white square and a multiplication sign at FontSize 12),
        // this one with the Phosphor Minus, Square and X.
        //
        // Sized from what Avalonia actually puts on screen rather than from
        // its FontSize, because a glyph fills its em box loosely and an icon
        // fills its box exactly. The three are genuinely different sizes;
        // measured against a 40px bar, and scaled to this 32px one:
        //
        //             Avalonia      here
        //   dash       12 x 2        10
        //   square     10 x 9         8
        //   cross       8 x 9         7
        static const int CAPTION_DASH_PX = 10;
        static const int CAPTION_SQUARE_PX = 8;
        static const int CAPTION_CROSS_PX = 7;

        static const ChromeMetrics g_metrics = {
            CHROME_H,
            FOOTER_H,
            6,   // resize_border — grab width of the window edges
            640, // min_w
            480  // min_h
        };

        const ChromeMetrics &chrome_metrics() { return g_metrics; }

        // "12.3 MB/s". One decimal at MB and above, none below: the footer
        // has one line for this and the extra digit is noise at KB rates.
        static void format_rate(unsigned long bytes_per_sec, char *out, int out_sz)
        {
            (void)out_sz;

            const double bps = (double)bytes_per_sec;

            if (bps >= 1024.0 * 1024.0)
                sprintf(out, "%.1f MB/s", bps / (1024.0 * 1024.0));
            else if (bps >= 1024.0)
                sprintf(out, "%.0f KB/s", bps / 1024.0);
            else
                sprintf(out, "%.0f B/s", bps);
        }

        int chrome_height() { return CHROME_H; }

        namespace
        {
            // True on the pages that show a collection of games, which are the
            // only ones where Refresh and the Library/Depot switch mean
            // anything.
            bool on_games_screen(App &app)
            {
                const Screen s = app.current_screen();
                return s == Screen::Library || s == Screen::Depot ||
                       s == Screen::DepotBrowse;
            }

            bool on_depot(App &app)
            {
                const Screen s = app.current_screen();
                return s == Screen::Depot || s == Screen::DepotBrowse;
            }

            // True once past sign-in, which is what MainWindowViewModel calls
            // IsShellActive. Server select and login own the whole window and
            // carry neither the logo nor a view title.
            bool shell_active(App &app)
            {
                const Screen s = app.current_screen();
                return s != Screen::ServerSelect && s != Screen::Login;
            }

            // What the title bar names the screen you are on.
            //
            // The mapping is ShellViewModel.ContentViewTitle's, case for case,
            // including its habit of returning an empty string rather than a
            // placeholder while a game detail page is still loading.
            std::string content_view_title(App &app)
            {
                switch (app.current_screen())
                {
                case Screen::Library:
                    return "My Library";

                case Screen::Depot:
                    return "Depot";

                case Screen::DepotBrowse:
                    // DepotBrowseViewModel.BrowseTitle: the thing filtered on,
                    // the search terms, or the unfiltered catalogue.
                    switch (app.depot_filter_kind())
                    {
                    case DepotFilterKind::Genre:
                    case DepotFilterKind::Collection:
                        return app.depot_filter_value();
                    case DepotFilterKind::Search:
                        return "Search: " + app.depot_filter_value();
                    default:
                        return "All Games";
                    }

                case Screen::GameDetail:
                    return screen_game_detail_title();

                case Screen::Downloads:
                    return "Downloads";

                case Screen::Settings:
                    return "Settings";

                case Screen::ScriptConsole:
                    // The one screen with no entry in that table: Avalonia
                    // puts the console in a separate window, which carries its
                    // own title rather than borrowing the shell's. Naming it
                    // here beats leaving the bar blank on a page that is
                    // otherwise indistinguishable from a broken one.
                    return "Script Console";

                default:
                    return std::string();
                }
            }
        } // namespace

        int footer_height() { return FOOTER_H; }

        bool footer_visible(App &app)
        {
            switch (app.current_screen())
            {
            case Screen::Library:
            case Screen::Depot:
            case Screen::DepotBrowse:
            case Screen::GameDetail:
            case Screen::Downloads:
            case Screen::Settings:
                return true;
            default:
                // Login and server select own the whole window; blocking a
                // strip at the bottom would make their buttons unclickable.
                return false;
            }
        }

        // Screen rectangle of the open profile dropdown, or a zero rect.
        //
        // Recorded during the draw and read by chrome_gate() on the NEXT
        // frame, which is exactly right: on the frame the menu opens, the
        // click was on the button in the title bar, and that is already
        // covered by the title-bar band.
        static gfx::Rect s_menu_rect = { 0, 0, 0, 0 };

        InputState chrome_gate(App &app, const InputState &input)
        {
            const int sh = app.screen_height();

            const bool over_title = (input.mouse.y >= 0 && input.mouse.y < CHROME_H);

            const bool over_footer = footer_visible(app) &&
                                     input.mouse.y >= sh - FOOTER_H &&
                                     input.mouse.y < sh;

            const bool over_menu =
                s_menu_rect.w > 0 && s_menu_rect.h > 0 &&
                gfx::rect_contains(s_menu_rect, input.mouse.x, input.mouse.y);

            if (over_title || over_footer || over_menu)
                return input_without_pointer(input);

            return input;
        }


        // ---------------------------------------------------------------
        // Title bar icon — loaded once from the EXE resource.
        // ---------------------------------------------------------------
        static gfx::Surface *s_icon_bmp = NULL;
        static bool s_icon_loaded = false;
        static const int ICON_SIZE = 19; // Avalonia's 24 in a 40px bar

        // The title bar's own type size, and the profile button's.
        static const FontSize TITLE_FONT = FontSize::Small;

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

        // ---------------------------------------------------------------
        // Profile avatar -- decoded once from the file App downloaded.
        // ---------------------------------------------------------------
        // Avalonia's avatar Panel is 22 square inside a 40px title bar; at
        // this bar's 32 that is 18.
        static const int AVATAR_SIZE = 18;
        static const int AVATAR_RADIUS = 2;  // matches the Avalonia CornerRadius

        static gfx::Surface *s_avatar = NULL;
        static std::string s_avatar_source;  // path the cached surface came from

        // NULL when the user has no avatar or it could not be decoded.
        //
        // Keyed on the path so a re-login as a different user reloads rather
        // than showing the previous person's picture.
        static gfx::Surface *avatar_surface(App &app)
        {
            if (!app.has_avatar())
            {
                if (s_avatar)
                {
                    gfx::destroy_surface(s_avatar);
                    s_avatar = NULL;
                }
                s_avatar_source.clear();
                return NULL;
            }

            if (s_avatar && s_avatar_source == app.avatar_path())
                return s_avatar;

            if (s_avatar)
            {
                gfx::destroy_surface(s_avatar);
                s_avatar = NULL;
            }

            s_avatar_source = app.avatar_path();

            // Aspect-FILL so a non-square picture is cropped rather than
            // letterboxed, matching the Avalonia Stretch="UniformToFill".
            DecodedImage img;
            if (decode_image_file_fill(s_avatar_source.c_str(),
                                       AVATAR_SIZE, AVATAR_SIZE, &img))
            {
                s_avatar = gfx::surface_from_rgba(img.pixels, img.width, img.height);
                free_decoded_image(&img);
            }

            return s_avatar;
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
            //
            // Both are hidden on the two screens that own the whole window,
            // matching MainWindowViewModel.IsLogoVisible (false on server
            // select and login) and the title's IsShellActive binding.
            if (shell_active(app))
            {
                ensure_icon_loaded();

                // Avalonia's logo Border is Padding="8,0" around a 24px image
                // and the title's is Padding="4,0,12,0"; at 0.8 that is a 6px
                // inset, a 19px icon and a 3px gap.
                int title_x = 6;
                if (s_icon_bmp)
                {
                    int icon_y = (CHROME_H - ICON_SIZE) / 2;
                    gfx::blit(buf, s_icon_bmp, title_x, icon_y);
                    title_x += ICON_SIZE + 3;
                }

                // The view's name, not the program's. The Avalonia title bar
                // binds ShellViewModel.ContentViewTitle here, so it reads
                // "My Library", "Settings", or the game you are looking at;
                // this said "LANCommander" on every screen, which is the one
                // thing the window is already unambiguously called.
                //
                // Small rung: that label is FontSize 13 against a base of 16.
                const std::string title = content_view_title(app);
                if (!title.empty())
                    draw_text(buf, title_x, (CHROME_H - text_height(TITLE_FONT)) / 2,
                              theme().text_bright, title.c_str(), TITLE_FONT);
            }

            // --- Close button (right edge) ---
            int close_x = sw - BTN_W;
            {
                bool hovered = (input.mouse.x >= close_x && input.mouse.x < close_x + BTN_W &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);
                if (hovered)
                    panel(buf, close_x, 0, BTN_W, CHROME_H,
                          gfx::rgb(0xC4, 0x2B, 0x1C)); // Windows red close

                // Body text, not dim: Avalonia leaves these on the default
                // foreground, and against the title bar tint the dim grey
                // read as disabled.
                const gfx::Color color = hovered ? theme().text_bright : theme().text;
                draw_icon_centered(buf, gfx::rect(close_x, 0, BTN_W, CHROME_H),
                                   CAPTION_CROSS_PX, color, Icon::Close);

                if (hovered && input.mouse.pressed)
                    close_clicked = true;
            }

            // --- Maximize / restore button ---
            //
            // Same square in both states, as the Avalonia title bar does —
            // its MaximizeButton_Click flips WindowState but leaves the
            // glyph alone, so there is no restore icon to swap in.
            int max_x = close_x - BTN_W;
            {
                bool hovered = (input.mouse.x >= max_x && input.mouse.x < max_x + BTN_W &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);
                if (hovered)
                    panel(buf, max_x, 0, BTN_W, CHROME_H, theme().button_bg_hover);

                const gfx::Color color = hovered ? theme().text_bright : theme().text;
                draw_icon_centered(buf, gfx::rect(max_x, 0, BTN_W, CHROME_H),
                                   CAPTION_SQUARE_PX, color, Icon::Maximize);

                if (hovered && input.mouse.pressed)
                    chrome_platform_maximize_toggle();
            }

            // --- Minimize button ---
            int min_x = max_x - BTN_W;
            {
                bool hovered = (input.mouse.x >= min_x && input.mouse.x < min_x + BTN_W &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);
                if (hovered)
                    panel(buf, min_x, 0, BTN_W, CHROME_H, theme().button_bg_hover);

                const gfx::Color color = hovered ? theme().text_bright : theme().text;
                draw_icon_centered(buf, gfx::rect(min_x, 0, BTN_W, CHROME_H),
                                   CAPTION_DASH_PX, color, Icon::Minimize);

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
                // Avatar then alias, and nothing else.
                //
                // There was a person glyph on the left and a caret on the
                // right; both are gone. The Avalonia button is the user's
                // picture beside their name with no disclosure arrow, and it
                // keeps a User icon only as a placeholder for an account with
                // no picture at all.
                // Avalonia's Padding 8,4 with an inner Spacing of 8, at 0.8.
                // The button is INSET in the bar rather than filling it: it is
                // a Button.Primary sitting in a 40px title bar with 6px of
                // clearance above and below, not a block welded to the top
                // edge, which is what a full-height fill looked like.
                const int pad = 6;
                const int gap = 6;
                const int th = text_height(TITLE_FONT);
                const int alias_w = text_width(alias_str.c_str(), TITLE_FONT);

                gfx::Surface *avatar = avatar_surface(app);
                const int avatar_w = avatar ? AVATAR_SIZE : 0;
                const int avatar_gap = avatar ? gap : 0;

                const int btn_h = AVATAR_SIZE + pad;      // 24 in a 32px bar
                const int btn_y = (CHROME_H - btn_h) / 2;

                user_btn_w = pad + avatar_w + avatar_gap + alias_w + pad;
                user_btn_x = user_btn_right - user_btn_w;

                // Hit-tested over the whole bar height, not just the pill:
                // the clearance above and below it belongs to no one else,
                // and a 4px dead strip around a button is a miss waiting to
                // happen.
                bool hovered = (input.mouse.x >= user_btn_x && input.mouse.x < user_btn_right &&
                                input.mouse.y >= 0 && input.mouse.y < CHROME_H);

                // Background — always primary blue, darker when active.
                gfx::Color bg = (s_user_dropdown_open || hovered)
                                    ? theme().button_primary_active
                                    : theme().button_primary;
                fill_rounded_rect(buf, gfx::rect(user_btn_x, btn_y, user_btn_w, btn_h),
                                  BUTTON_RADIUS, bg);

                int cx = user_btn_x + pad;

                if (avatar)
                {
                    const gfx::Rect box = gfx::rect(cx, btn_y + (btn_h - AVATAR_SIZE) / 2,
                                                    AVATAR_SIZE, AVATAR_SIZE);

                    // The decode covers the box, so this is the centre crop.
                    const int aw = gfx::surface_width(avatar);
                    const int ah = gfx::surface_height(avatar);
                    const int cw = box.w < aw ? box.w : aw;
                    const int ch = box.h < ah ? box.h : ah;

                    gfx::blit_region(buf, avatar,
                                     gfx::rect((aw - cw) / 2, (ah - ch) / 2, cw, ch),
                                     box.x + (box.w - cw) / 2,
                                     box.y + (box.h - ch) / 2);

                    // Corners painted back to the button fill, which is what
                    // stands in for a rounded clip here.
                    round_rect_corners(buf, box, AVATAR_RADIUS, bg);

                    cx += avatar_w + avatar_gap;
                }

                draw_text(buf, cx, btn_y + (btn_h - th) / 2, theme().text_bright,
                          alias_str.c_str(), TITLE_FONT);

                // Toggle on click.
                if (hovered && input.mouse.clicked)
                    s_user_dropdown_open = !s_user_dropdown_open;
            }

            // --- Refresh button (left of the profile button) ---
            //
            // Same place the Avalonia shell puts it, and for the same reason:
            // it refetches the whole view, so it belongs with the window's
            // controls rather than pinned under the sidebar list it used to
            // sit beneath, where it read as "refresh this list".
            int refresh_x = (user_btn_w > 0) ? user_btn_x : min_x;

            if (on_games_screen(app))
            {
                // Square, like the Avalonia IconButton's 40x40 — it used to
                // borrow the caption buttons' wider cell, which put the
                // refresh glyph off the rhythm of the icons beside it.
                refresh_x -= CHROME_H;

                const gfx::Rect r = gfx::rect(refresh_x, 0, CHROME_H, CHROME_H);
                const bool hovered = gfx::rect_contains(r, input.mouse.x, input.mouse.y);

                if (hovered)
                    gfx::fill_rect_alpha(buf, r, theme().ghost_hover);

                draw_icon_centered(buf, r, ICON_MD,
                                   hovered ? theme().text_bright : theme().text,
                                   Icon::Refresh);

                if (hovered && input.mouse.clicked)
                {
                    // Both collections, not just the visible one: the library
                    // page reads collection names out of the depot payload,
                    // and the depot's hero art comes from the games call the
                    // library makes. Refreshing one and not the other leaves
                    // the page half stale.
                    app.invalidate_depot();
                    app.invalidate_library();
                }
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

                // Published so chrome_gate() can keep the screen underneath
                // from seeing clicks aimed at this menu.
                s_menu_rect = gfx::rect(menu_x, menu_y, menu_w, menu_h);

                // Close menu when clicking outside.
                bool in_menu = (input.mouse.x >= menu_x && input.mouse.x < menu_x + menu_w &&
                                input.mouse.y >= menu_y && input.mouse.y < menu_y + menu_h);
                bool in_button = (input.mouse.x >= user_btn_x && input.mouse.x < user_btn_right &&
                                  input.mouse.y >= 0 && input.mouse.y < CHROME_H);

                if (input.mouse.clicked && !in_menu && !in_button)
                    s_user_dropdown_open = false;
            }

            if (!s_user_dropdown_open)
                s_menu_rect = gfx::rect(0, 0, 0, 0);

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
            const int drag_right = refresh_x;
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

            // Avalonia's footer buttons are the default Padding 12,6 at 0.8,
            // in a 52px bar at 0.8.
            int pad = 10;
            int btn_h = button_height();
            int btn_y = fy + (FOOTER_H - btn_h) / 2;

            // --- Left: Library / Depot switch ---
            //
            // One button, labelled with where it GOES, matching the Avalonia
            // shell footer. It briefly lived as a pair of tabs at the top of
            // the library sidebar; that showed both destinations at once and
            // pushed the compact list down, which is neither what the Avalonia
            // launcher does nor where anyone looks for it.
            if (on_games_screen(app))
            {
                const bool depot_active = on_depot(app);
                const char *label = depot_active ? "Library" : "Depot";
                const Icon icon = depot_active ? Icon::Library : Icon::Depot;

                const int gap = 8;   // Avalonia IconButton: (Size / 2) + 2
                const gfx::Rect r = gfx::rect(pad, btn_y,
                                              BUTTON_PAD_X * 2 + ICON_MD + gap +
                                                  text_width(label),
                                              btn_h);

                const bool hovered = gfx::rect_contains(r, input.mouse.x, input.mouse.y);

                if (hovered)
                    fill_rounded_rect_alpha(buf, r, BUTTON_RADIUS, theme().ghost_hover);

                const gfx::Color fg = hovered ? theme().text_bright : theme().text;

                draw_icon(buf, r.x + BUTTON_PAD_X, btn_y + (btn_h - ICON_MD) / 2,
                          ICON_MD, fg, icon);
                draw_text(buf, r.x + BUTTON_PAD_X + ICON_MD + gap,
                          btn_y + (btn_h - th) / 2, fg, label);

                if (hovered && input.mouse.clicked)
                    app.switch_screen(depot_active ? Screen::Library : Screen::Depot);
            }

            // --- Center: Download progress or Downloads button ---
            {
                const DownloadItem *cur = app.downloads().current_item();
                bool on_downloads_screen = (app.current_screen() == Screen::Downloads);

                if (cur && (cur->status == DownloadStatus::Downloading ||
                            cur->status == DownloadStatus::Extracting ||
                            cur->status == DownloadStatus::RunningScripts))
                {
                    // Show active download: title + progress bar + percentage.
                    // Clicking navigates to the Downloads screen.
                    int cx = sw / 2;
                    int info_w = 360;
                    int info_x = cx - info_w / 2;

                    bool area_hovered = (input.mouse.x >= info_x && input.mouse.x < info_x + info_w &&
                                         input.mouse.y >= fy && input.mouse.y < fy + FOOTER_H);

                    // Percentage, and the transfer rate beside it while there
                    // is one. The footer is on every screen, so this is where
                    // a download is watched from -- the Downloads screen is
                    // somewhere the user goes deliberately.
                    char pct[48];
                    if (cur->status == DownloadStatus::Downloading &&
                        cur->speed_bps > 0)
                    {
                        char rate[32];
                        format_rate(cur->speed_bps, rate, sizeof(rate));
                        sprintf(pct, "%s  %d%%", rate, (int)(cur->progress * 100));
                    }
                    else
                    {
                        sprintf(pct, "%d%%", (int)(cur->progress * 100));
                    }

                    // Title (left), clipped short of the figure on the right
                    // rather than at a fixed inset, which the rate would
                    // otherwise run into on a long game name.
                    // The title is the Small rung and the figures beside it
                    // Caption, as in the Avalonia footer where they are 13 and
                    // 11 against a base of 16.
                    const int pct_w = text_width(pct, FontSize::Caption);
                    gfx::push_clip(buf, gfx::rect(info_x, fy,
                                                  info_w - pct_w - 12, FOOTER_H));
                    draw_text(buf, info_x, btn_y + 1, theme().text,
                              cur->title.c_str(), FontSize::Small);
                    gfx::pop_clip(buf);

                    draw_text_right(buf, info_x + info_w, btn_y + 1,
                                    theme().text_dim, pct, FontSize::Caption);

                    // Progress bar
                    int bar_x = info_x;
                    int bar_w = info_w;
                    int bar_y2 = btn_y + btn_h + 1;
                    gfx::fill_rect(buf, gfx::rect(bar_x, bar_y2, bar_w, 3), theme().panel);
                    int fill = (int)(cur->progress * bar_w);
                    if (fill > 0)
                        gfx::fill_rect(buf, gfx::rect(bar_x, bar_y2, fill, 3),
                                       cur->status == DownloadStatus::Downloading
                                           ? theme().primary
                                           : theme().warning);

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

                    const int gap = 8;
                    int dl_w = BUTTON_PAD_X * 2 + ICON_MD + gap + text_width(dl_label);
                    int dl_x = sw / 2 - dl_w / 2;

                    const gfx::Rect dl_r = gfx::rect(dl_x, btn_y, dl_w, btn_h);
                    bool dl_hovered = gfx::rect_contains(dl_r, input.mouse.x, input.mouse.y);

                    if (on_downloads_screen)
                        fill_rounded_rect(buf, dl_r, BUTTON_RADIUS, theme().button_primary);
                    else if (dl_hovered)
                        fill_rounded_rect_alpha(buf, dl_r, BUTTON_RADIUS, theme().ghost_hover);

                    // Body text when idle, not disabled grey. Avalonia leaves
                    // this button on the default foreground — it is always
                    // clickable, and dimming it said otherwise.
                    const gfx::Color dl_fg = (on_downloads_screen || dl_hovered)
                                                 ? theme().text_bright
                                                 : theme().text;

                    draw_icon(buf, dl_x + BUTTON_PAD_X, btn_y + (btn_h - ICON_MD) / 2,
                              ICON_MD, dl_fg, Icon::Download);
                    draw_text(buf, dl_x + BUTTON_PAD_X + ICON_MD + gap,
                              btn_y + (btn_h - th) / 2, dl_fg, dl_label);

                    if (dl_hovered && input.mouse.clicked && !on_downloads_screen)
                        app.switch_screen(Screen::Downloads);
                }
            }

        }

    } // namespace ui
} // namespace launcher
