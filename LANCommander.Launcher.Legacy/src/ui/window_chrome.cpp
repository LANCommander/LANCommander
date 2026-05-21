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
#include <dwmapi.h>

namespace launcher
{
    namespace ui
    {

        static const int CHROME_H = 32;
        static const int FOOTER_H = 40;
        static const int BTN_W = 36;

#ifdef ALLEGRO_WINDOWS
        typedef LRESULT(CALLBACK *WndProcFn)(HWND, UINT, WPARAM, LPARAM);
        static WndProcFn s_orig_wndproc = NULL;

        static LRESULT CALLBACK chrome_wndproc(HWND hwnd, UINT msg,
                                               WPARAM wp, LPARAM lp)
        {
            switch (msg)
            {
            case WM_NCHITTEST:
                return HTCLIENT;
            case WM_NCCALCSIZE:
                if (wp) return 0;
                break;
            case WM_SETCURSOR:
                SetCursor(LoadCursor(NULL, IDC_ARROW));
                return TRUE;
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

        void chrome_remove_frame()
        {
#ifdef ALLEGRO_WINDOWS
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

            DWMNCRENDERINGPOLICY policy = DWMNCRP_DISABLED;
            DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY,
                                  &policy, sizeof(policy));

            SetWindowPos(hwnd, NULL, wr.left, wr.top, cw, ch,
                         SWP_NOZORDER | SWP_FRAMECHANGED);
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
        // Footer bar: Depot/Library toggle + download progress
        // ---------------------------------------------------------------
        static bool s_queue_expanded = false;

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

            // ==== Expanded download queue panel (above footer) ====
            if (s_queue_expanded)
            {
                const std::vector<DownloadItem> &items = app.downloads().items();
                int item_h = 36;
                int panel_h = (int)items.size() * item_h + 8;
                if (panel_h < 44) panel_h = 44;
                if (panel_h > 250) panel_h = 250;

                int py = fy - panel_h;
                panel(buf, 0, py, sw, panel_h, theme().surface);
                hline(buf, 0, py, sw - 1, theme().divider);

                if (items.empty())
                {
                    draw_text_center(buf, sw / 2, py + (panel_h - th) / 2,
                                     theme().text_disabled, "No downloads");
                }
                else
                {
                    int iy = py + 4;
                    for (size_t i = 0; i < items.size() && iy + item_h <= fy; ++i)
                    {
                        const DownloadItem &it = items[i];

                        // Title
                        draw_text(buf, 12, iy + 2, theme().text, it.title.c_str());

                        // Status + progress
                        const char *status_str = "Queued";
                        int status_color = theme().text_dim;
                        switch (it.status)
                        {
                        case DownloadStatus::Downloading:
                            status_str = "Downloading";
                            status_color = theme().primary;
                            break;
                        case DownloadStatus::Extracting:
                            status_str = "Extracting";
                            status_color = theme().warning;
                            break;
                        case DownloadStatus::Complete:
                            status_str = "Complete";
                            status_color = theme().success;
                            break;
                        case DownloadStatus::Failed:
                            status_str = "Failed";
                            status_color = theme().error;
                            break;
                        default:
                            break;
                        }

                        // Status text + bytes
                        char info[128];
                        if (it.status == DownloadStatus::Downloading && it.total > 0)
                        {
                            char recv_s[32], total_s[32];
                            format_bytes(it.received, recv_s, sizeof(recv_s));
                            format_bytes(it.total, total_s, sizeof(total_s));
                            sprintf(info, "%s  %s / %s  (%d%%)",
                                    status_str, recv_s, total_s,
                                    (int)(it.progress * 100));
                        }
                        else
                            sprintf(info, "%s", status_str);

                        draw_text(buf, 12, iy + 2 + th + 2, status_color, info);

                        // Progress bar
                        if (it.status == DownloadStatus::Downloading)
                        {
                            int bar_x = sw - 200 - 12;
                            int bar_w = 200;
                            int bar_y2 = iy + item_h - 8;
                            rectfill(buf, bar_x, bar_y2, bar_x + bar_w - 1, bar_y2 + 3,
                                     theme().panel);
                            int fill = (int)(it.progress * bar_w);
                            if (fill > 0)
                                rectfill(buf, bar_x, bar_y2, bar_x + fill - 1, bar_y2 + 3,
                                         theme().primary);
                        }

                        iy += item_h;
                        if (i + 1 < items.size())
                            hline(buf, 8, iy - 1, sw - 8, theme().divider);
                    }
                }
            }

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
                    app.set_library_tab(LibraryTab::Depot);

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
                    app.set_library_tab(LibraryTab::Library);
            }

            // --- Center: Download progress or button ---
            {
                const DownloadItem *cur = app.downloads().current_item();
                if (cur && (cur->status == DownloadStatus::Downloading ||
                            cur->status == DownloadStatus::Extracting))
                {
                    // Show active download: title + progress bar + percentage.
                    int cx = sw / 2;
                    int info_w = 300;
                    int info_x = cx - info_w / 2;

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
                }
                else
                {
                    // "Downloads" button with pending count badge.
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

                    if (dl_hovered)
                        rectfill(buf, dl_x, btn_y, dl_x + dl_w - 1, btn_y + btn_h - 1,
                                 theme().panel_hover);

                    draw_text_center(buf, dl_x + dl_w / 2, btn_y + (btn_h - th) / 2,
                                     dl_hovered ? theme().text : theme().text_disabled,
                                     dl_label);

                    if (dl_hovered && input.mouse.clicked)
                        s_queue_expanded = !s_queue_expanded;
                }
            }
        }

    } // namespace ui
} // namespace launcher
