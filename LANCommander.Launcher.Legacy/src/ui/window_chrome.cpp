// Allegro 4 must come before Windows headers.
#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "ui/window_chrome.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "app/app.h"

#include <windows.h>
#include <cstdio>

// DWM is Vista+ only. Dynamically load to keep Win9x compatibility.
typedef HRESULT (WINAPI *PFN_DwmSetWindowAttribute)(HWND, DWORD, LPCVOID, DWORD);
static PFN_DwmSetWindowAttribute s_pfnDwmSetWindowAttribute = NULL;
static bool s_dwm_checked = false;

static void ensure_dwm()
{
    if (s_dwm_checked) return;
    s_dwm_checked = true;
    HMODULE hDwm = LoadLibraryA("dwmapi.dll");
    if (hDwm)
        s_pfnDwmSetWindowAttribute = (PFN_DwmSetWindowAttribute)
            GetProcAddress(hDwm, "DwmSetWindowAttribute");
}

// DWM constants (avoid requiring dwmapi.h)
#define LC_DWMWA_NCRENDERING_POLICY 2
#define LC_DWMNCRP_DISABLED         1

namespace launcher
{
    namespace ui
    {

        static const int CHROME_H = 32;
        static const int FOOTER_H = 40;
        static const int BTN_W = 36;
        static const int RESIZE_BORDER = 6;
        static const int MIN_W = 640;
        static const int MIN_H = 480;

#ifdef ALLEGRO_WINDOWS
        typedef LRESULT(CALLBACK *WndProcFn)(HWND, UINT, WPARAM, LPARAM);
        static WndProcFn s_orig_wndproc = NULL;
        static App *s_app = NULL;

        static LRESULT chrome_hittest(HWND hwnd, LPARAM lp)
        {
            POINT pt = { (short)LOWORD(lp), (short)HIWORD(lp) };
            RECT rc;
            GetClientRect(hwnd, &rc);
            ScreenToClient(hwnd, &pt);

            int w = rc.right;
            int h = rc.bottom;

            bool top    = pt.y < RESIZE_BORDER;
            bool bottom = pt.y >= h - RESIZE_BORDER;
            bool left   = pt.x < RESIZE_BORDER;
            bool right  = pt.x >= w - RESIZE_BORDER;

            if (top && left)     return HTTOPLEFT;
            if (top && right)    return HTTOPRIGHT;
            if (bottom && left)  return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (top)             return HTTOP;
            if (bottom)          return HTBOTTOM;
            if (left)            return HTLEFT;
            if (right)           return HTRIGHT;

            return HTCLIENT;
        }

        static LRESULT CALLBACK chrome_wndproc(HWND hwnd, UINT msg,
                                               WPARAM wp, LPARAM lp)
        {
            switch (msg)
            {
            case WM_NCHITTEST:
                return chrome_hittest(hwnd, lp);

            case WM_NCCALCSIZE:
                if (wp) return 0;
                break;

            case WM_SETCURSOR:
            {
                // Show resize cursors at window edges, arrow elsewhere.
                LRESULT ht = chrome_hittest(hwnd, GetMessagePos());
                LPCSTR cur = IDC_ARROW;
                switch (ht)
                {
                case HTLEFT: case HTRIGHT:           cur = IDC_SIZEWE;   break;
                case HTTOP: case HTBOTTOM:            cur = IDC_SIZENS;   break;
                case HTTOPLEFT: case HTBOTTOMRIGHT:   cur = IDC_SIZENWSE; break;
                case HTTOPRIGHT: case HTBOTTOMLEFT:   cur = IDC_SIZENESW; break;
                }
                SetCursor(LoadCursor(NULL, cur));
                return TRUE;
            }

            case WM_GETMINMAXINFO:
            {
                MINMAXINFO *mmi = (MINMAXINFO *)lp;
                mmi->ptMinTrackSize.x = MIN_W;
                mmi->ptMinTrackSize.y = MIN_H;
                return 0;
            }

            case WM_SIZE:
            {
                if (wp != SIZE_MINIMIZED && s_app)
                {
                    int new_w = LOWORD(lp);
                    int new_h = HIWORD(lp);
                    if (new_w > 0 && new_h > 0)
                        s_app->request_resize(new_w, new_h);
                }
                break;
            }

            case WM_ERASEBKGND:
                // Suppress background erase — we paint the entire client area.
                return 1;

            case WM_PAINT:
            {
                // Prevent Allegro's default WM_PAINT from blitting its
                // stale 800x600 screen surface. Just validate the
                // region — our main loop repaints every frame.
                PAINTSTRUCT ps;
                BeginPaint(hwnd, &ps);
                if (s_app && s_app->backbuffer())
                {
                    blit_to_hdc(s_app->backbuffer(), ps.hdc,
                                0, 0, 0, 0,
                                s_app->screen_width(),
                                s_app->screen_height());
                }
                EndPaint(hwnd, &ps);
                return 0;
            }
            }
            if (s_orig_wndproc)
                return s_orig_wndproc(hwnd, msg, wp, lp);
            return DefWindowProc(hwnd, msg, wp, lp);
        }

        static void ensure_subclass()
        {
            HWND hwnd = win_get_window();
            if (!hwnd) return;
            WndProcFn current = (WndProcFn)GetWindowLongPtr(hwnd, GWLP_WNDPROC);
            if (current == chrome_wndproc) return;
            s_orig_wndproc = current;
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, (LONG_PTR)chrome_wndproc);
        }
#endif

        int chrome_height() { return CHROME_H; }
        int footer_height() { return FOOTER_H; }

        void chrome_remove_frame(App *app)
        {
#ifdef ALLEGRO_WINDOWS
            s_app = app;

            HWND hwnd = win_get_window();
            if (!hwnd) return;

            RECT client;
            GetClientRect(hwnd, &client);
            int cw = client.right - client.left;
            int ch = client.bottom - client.top;

            RECT wr;
            GetWindowRect(hwnd, &wr);

            LONG style = GetWindowLong(hwnd, GWL_STYLE);
            style &= ~(WS_CAPTION | WS_THICKFRAME | WS_SYSMENU |
                        WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            style |= WS_POPUP;
            SetWindowLong(hwnd, GWL_STYLE, style);

            LONG exstyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exstyle &= ~(WS_EX_DLGMODALFRAME | WS_EX_CLIENTEDGE |
                         WS_EX_STATICEDGE | WS_EX_WINDOWEDGE);
            SetWindowLong(hwnd, GWL_EXSTYLE, exstyle);

            // Disable DWM non-client rendering (Vista+ only, no-op on Win9x/XP)
            ensure_dwm();
            if (s_pfnDwmSetWindowAttribute)
            {
                DWORD policy = LC_DWMNCRP_DISABLED;
                s_pfnDwmSetWindowAttribute(hwnd, LC_DWMWA_NCRENDERING_POLICY,
                                           &policy, sizeof(policy));
            }

            // Install the WndProc subclass BEFORE SetWindowPos so that
            // the WM_NCCALCSIZE triggered by SWP_FRAMECHANGED is handled
            // by our proc (returns 0 → no non-client area).
            ensure_subclass();

            SetWindowPos(hwnd, NULL, wr.left, wr.top, cw, ch,
                         SWP_NOZORDER | SWP_FRAMECHANGED);

            // Re-assert foreground + focus — the style change to WS_POPUP
            // can cause the window to lose keyboard focus on some systems.
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
#endif
        }

        // ---------------------------------------------------------------
        // Semi-transparent black overlay for the title bar area.
        // ---------------------------------------------------------------
        static void draw_tint(BITMAP *buf, int x, int y, int w, int h,
                              int r, int g, int b, int alpha)
        {
            for (int row = 0; row < h; row++)
            {
                for (int col = 0; col < w; col++)
                {
                    int px = getpixel(buf, x + col, y + row);
                    int pr = getr(px);
                    int pg = getg(px);
                    int pb = getb(px);
                    int nr = pr + (r - pr) * alpha / 255;
                    int ng = pg + (g - pg) * alpha / 255;
                    int nb = pb + (b - pb) * alpha / 255;
                    putpixel(buf, x + col, y + row, makecol(nr, ng, nb));
                }
            }
        }

        // ---------------------------------------------------------------
        // Title bar icon — loaded once from the EXE resource.
        // ---------------------------------------------------------------
        static BITMAP *s_icon_bmp = NULL;
        static bool s_icon_loaded = false;
        static const int ICON_SIZE = 20; // display size in the title bar

        static void ensure_icon_loaded()
        {
            if (s_icon_loaded) return;
            s_icon_loaded = true;

#ifdef ALLEGRO_WINDOWS
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

            // Create an Allegro bitmap from the BGRA pixels.
            s_icon_bmp = create_bitmap_ex(32, ICON_SIZE, ICON_SIZE);
            if (s_icon_bmp)
            {
                for (int y = 0; y < ICON_SIZE; y++)
                {
                    for (int x = 0; x < ICON_SIZE; x++)
                    {
                        int idx = (y * ICON_SIZE + x) * 4;
                        int b = pixels[idx + 0];
                        int g = pixels[idx + 1];
                        int r = pixels[idx + 2];
                        int a = pixels[idx + 3];

                        // Pre-multiply against black background for
                        // simple blit (no alpha blending needed).
                        r = r * a / 255;
                        g = g * a / 255;
                        b = b * a / 255;

                        putpixel(s_icon_bmp, x, y, makecol32(r, g, b));
                    }
                }
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
#ifdef ALLEGRO_WINDOWS
            ensure_subclass();
#endif

            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();
            bool close_clicked = false;

            // --- Semi-transparent black overlay (50% opacity) ---
            draw_tint(buf, 0, 0, sw, CHROME_H, 0, 0, 0, 128);

            // --- Icon + Title ---
            ensure_icon_loaded();
            int title_x = 10;
            if (s_icon_bmp)
            {
                int icon_y = (CHROME_H - ICON_SIZE) / 2;
                blit(s_icon_bmp, buf, 0, 0, title_x, icon_y,
                     ICON_SIZE, ICON_SIZE);
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
                          makecol(0xC4, 0x2B, 0x1C)); // Windows red close

                int cx = close_x + BTN_W / 2;
                int color = hovered ? theme().text_bright : theme().text_dim;
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
                    draw_tint(buf, min_x, 0, BTN_W, CHROME_H, 255, 255, 255, 25);

                int cx = min_x + BTN_W / 2;
                int color = hovered ? theme().text_bright : theme().text_dim;
                draw_text_center(buf, cx, (CHROME_H - text_height()) / 2, color, "_");

                if (hovered && input.mouse.pressed)
                {
#ifdef ALLEGRO_WINDOWS
                    HWND hwnd = win_get_window();
                    if (hwnd) ShowWindow(hwnd, SW_MINIMIZE);
#endif
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
                int bg = (s_user_dropdown_open || hovered) ? theme().primary_active : theme().primary;
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
                rect(buf, menu_x, menu_y, menu_x + menu_w - 1, menu_y + menu_h - 1,
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
                        hline(buf, menu_x + 1, iy, menu_x + menu_w - 2, theme().divider);

                    int text_y = iy + (menu_item_h - text_height()) / 2;
                    int color = item_hovered ? theme().text_bright : theme().text;

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
            int drag_right = (user_btn_w > 0) ? user_btn_x : min_x;
            bool in_drag_area = (input.mouse.x >= 0 && input.mouse.x < drag_right &&
                                 input.mouse.y >= 0 && input.mouse.y < CHROME_H);

            if (in_drag_area && input.mouse.pressed && !s_user_dropdown_open)
            {
#ifdef ALLEGRO_WINDOWS
                HWND hwnd = win_get_window();
                if (hwnd)
                {
                    ReleaseCapture();
                    SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
#endif
            }

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
            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            int fy = sh - FOOTER_H;
            int th = text_height();

            // ==== Footer bar ====
            panel(buf, 0, fy, sw, FOOTER_H, theme().footer);
            hline(buf, 0, fy, sw - 1, theme().divider);

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
                    rectfill(buf, bx, btn_y, bx + bw - 1, btn_y + btn_h - 1, theme().panel_hover);

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
                    set_clip_rect(buf, info_x, fy, info_x + info_w - 80, fy + FOOTER_H);
                    draw_text(buf, info_x, btn_y + 1, theme().text, cur->title.c_str());
                    set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

                    // Percentage (right)
                    char pct[16];
                    sprintf(pct, "%d%%", (int)(cur->progress * 100));
                    draw_text_right(buf, info_x + info_w, btn_y + 1,
                                    theme().text_dim, pct);

                    // Progress bar
                    int bar_x = info_x;
                    int bar_w = info_w;
                    int bar_y2 = btn_y + btn_h + 1;
                    rectfill(buf, bar_x, bar_y2, bar_x + bar_w - 1, bar_y2 + 2,
                             theme().panel);
                    int fill = (int)(cur->progress * bar_w);
                    if (fill > 0)
                        rectfill(buf, bar_x, bar_y2, bar_x + fill - 1, bar_y2 + 2,
                                 theme().primary);

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
                        rectfill(buf, dl_x, btn_y, dl_x + dl_w - 1, btn_y + btn_h - 1,
                                 theme().primary);
                    else if (dl_hovered)
                        rectfill(buf, dl_x, btn_y, dl_x + dl_w - 1, btn_y + btn_h - 1,
                                 theme().panel_hover);

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
