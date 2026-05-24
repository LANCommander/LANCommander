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

        bool window_chrome_draw(App &app, const InputState &input)
        {
#ifdef ALLEGRO_WINDOWS
            ensure_subclass();
#endif

            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            bool close_clicked = false;

            // --- Semi-transparent black overlay (50% opacity) ---
            draw_tint(buf, 0, 0, sw, CHROME_H, 0, 0, 0, 128);

            // --- Title ---
            draw_text(buf, 10, (CHROME_H - text_height()) / 2,
                      theme().text_bright, "LANCommander");

            // --- Player alias (left of window buttons) ---
            int buttons_left = sw - BTN_W * 2;
            if (!app.user_alias().empty())
            {
                draw_text_right(buf, buttons_left - 12, (CHROME_H - text_height()) / 2,
                                theme().text_dim, app.user_alias().c_str());
            }

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

            // --- Drag handling ---
            int drag_right = min_x;
            bool in_drag_area = (input.mouse.x >= 0 && input.mouse.x < drag_right &&
                                 input.mouse.y >= 0 && input.mouse.y < CHROME_H);

            if (in_drag_area && input.mouse.pressed)
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

            // --- Left: Depot / Library toggle ---
            {
                const char *depot_label = "Depot";
                const char *lib_label = "Library";
                int depot_w = text_width(depot_label) + 20;
                int lib_w = text_width(lib_label) + 20;

                int dx = pad;
                bool depot_active = (app.library_tab() == LibraryTab::Depot);
                bool depot_hovered = (input.mouse.x >= dx && input.mouse.x < dx + depot_w &&
                                      input.mouse.y >= btn_y && input.mouse.y < btn_y + btn_h);

                if (depot_active)
                    rectfill(buf, dx, btn_y, dx + depot_w - 1, btn_y + btn_h - 1, theme().primary);
                else if (depot_hovered)
                    rectfill(buf, dx, btn_y, dx + depot_w - 1, btn_y + btn_h - 1, theme().panel_hover);

                draw_text_center(buf, dx + depot_w / 2, btn_y + (btn_h - th) / 2,
                                 depot_active ? theme().text_bright : theme().text_dim,
                                 depot_label);

                if (depot_hovered && input.mouse.clicked && !depot_active)
                {
                    app.set_library_tab(LibraryTab::Depot);
                    if (app.current_screen() != Screen::Library)
                        app.switch_screen(Screen::Library);
                }

                int lx = dx + depot_w + 4;
                bool lib_active = (app.library_tab() == LibraryTab::Library);
                bool lib_hovered = (input.mouse.x >= lx && input.mouse.x < lx + lib_w &&
                                    input.mouse.y >= btn_y && input.mouse.y < btn_y + btn_h);

                if (lib_active)
                    rectfill(buf, lx, btn_y, lx + lib_w - 1, btn_y + btn_h - 1, theme().primary);
                else if (lib_hovered)
                    rectfill(buf, lx, btn_y, lx + lib_w - 1, btn_y + btn_h - 1, theme().panel_hover);

                draw_text_center(buf, lx + lib_w / 2, btn_y + (btn_h - th) / 2,
                                 lib_active ? theme().text_bright : theme().text_dim,
                                 lib_label);

                if (lib_hovered && input.mouse.clicked && !lib_active)
                {
                    app.set_library_tab(LibraryTab::Library);
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

            // --- Right: Settings button ---
            {
                const char *settings_label = "Settings";
                int set_w = text_width(settings_label) + 20;
                int set_x = sw - pad - set_w;
                bool on_settings = (app.current_screen() == Screen::Settings);
                bool set_hovered = (input.mouse.x >= set_x && input.mouse.x < set_x + set_w &&
                                    input.mouse.y >= btn_y && input.mouse.y < btn_y + btn_h);

                if (on_settings)
                    rectfill(buf, set_x, btn_y, set_x + set_w - 1, btn_y + btn_h - 1,
                             theme().primary);
                else if (set_hovered)
                    rectfill(buf, set_x, btn_y, set_x + set_w - 1, btn_y + btn_h - 1,
                             theme().panel_hover);

                draw_text_center(buf, set_x + set_w / 2, btn_y + (btn_h - th) / 2,
                                 (on_settings || set_hovered)
                                     ? theme().text_bright : theme().text_dim,
                                 settings_label);

                if (set_hovered && input.mouse.clicked && !on_settings)
                    app.switch_screen(Screen::Settings);
            }
        }

    } // namespace ui
} // namespace launcher
