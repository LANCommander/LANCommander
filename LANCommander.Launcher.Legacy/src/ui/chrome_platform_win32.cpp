// chrome_platform_win32.cpp — frameless-window chrome via a WndProc subclass.
//
// Transitional: this is the Allegro-backend path. Allegro owns the window and
// its message loop, so the only way to get frameless behaviour is to strip the
// window styles and subclass its WndProc. The SDL backend does all of this
// through SDL_SetWindowHitTest instead (chrome_platform_sdl.cpp), and this
// file goes away with Allegro.

#include "ui/chrome_platform.h"
#include "ui/chrome_geometry.h"
#include "app/app.h"
#include "gfx/gfx.h"

#include <windows.h>

// DWM is Vista+ only. Dynamically load to keep Win9x compatibility.
typedef HRESULT(WINAPI *PFN_DwmSetWindowAttribute)(HWND, DWORD, LPCVOID, DWORD);
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

        namespace
        {
            typedef LRESULT(CALLBACK *WndProcFn)(HWND, UINT, WPARAM, LPARAM);

            WndProcFn s_orig_wndproc = NULL;
            App *s_app = NULL;
            int s_drag_right = 0;
            bool s_drag_enabled = true;

            // Shares chrome_geometry.cpp with the SDL hit test so the two
            // cannot drift; only the mapping onto Win32's HT* codes is here.
            //
            // Dragging is not reported through WM_NCHITTEST: this backend
            // starts the drag explicitly from chrome_platform_begin_drag(),
            // so Draggable maps to HTCLIENT and the UI keeps the click.
            LRESULT chrome_hittest(HWND hwnd, LPARAM lp)
            {
                POINT pt = { (short)LOWORD(lp), (short)HIWORD(lp) };
                RECT rc;
                GetClientRect(hwnd, &rc);
                ScreenToClient(hwnd, &pt);

                const ChromeMetrics &m = chrome_metrics();

                switch (chrome_hit_test(pt.x, pt.y, rc.right, rc.bottom,
                                        m.chrome_h, m.resize_border,
                                        s_drag_right, s_drag_enabled))
                {
                case ChromeHit::ResizeTopLeft:     return HTTOPLEFT;
                case ChromeHit::ResizeTopRight:    return HTTOPRIGHT;
                case ChromeHit::ResizeBottomLeft:  return HTBOTTOMLEFT;
                case ChromeHit::ResizeBottomRight: return HTBOTTOMRIGHT;
                case ChromeHit::ResizeTop:         return HTTOP;
                case ChromeHit::ResizeBottom:      return HTBOTTOM;
                case ChromeHit::ResizeLeft:        return HTLEFT;
                case ChromeHit::ResizeRight:       return HTRIGHT;
                default:                           return HTCLIENT;
                }
            }

            LRESULT CALLBACK chrome_wndproc(HWND hwnd, UINT msg,
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
                    case HTTOP: case HTBOTTOM:           cur = IDC_SIZENS;   break;
                    case HTTOPLEFT: case HTBOTTOMRIGHT:  cur = IDC_SIZENWSE; break;
                    case HTTOPRIGHT: case HTBOTTOMLEFT:  cur = IDC_SIZENESW; break;
                    }
                    SetCursor(LoadCursor(NULL, cur));
                    return TRUE;
                }

                case WM_GETMINMAXINFO:
                {
                    const ChromeMetrics &m = chrome_metrics();
                    MINMAXINFO *mmi = (MINMAXINFO *)lp;
                    mmi->ptMinTrackSize.x = m.min_w;
                    mmi->ptMinTrackSize.y = m.min_h;
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
                    // Stop Allegro's default WM_PAINT from blitting its stale
                    // screen surface; repaint from our own backbuffer instead.
                    PAINTSTRUCT ps;
                    BeginPaint(hwnd, &ps);
                    gfx::present();
                    EndPaint(hwnd, &ps);
                    return 0;
                }
                }

                if (s_orig_wndproc)
                    return s_orig_wndproc(hwnd, msg, wp, lp);
                return DefWindowProc(hwnd, msg, wp, lp);
            }

            void ensure_subclass()
            {
                HWND hwnd = (HWND)gfx::native_window_handle();
                if (!hwnd) return;
                WndProcFn current = (WndProcFn)GetWindowLongPtr(hwnd, GWLP_WNDPROC);
                if (current == chrome_wndproc) return;
                s_orig_wndproc = current;
                SetWindowLongPtr(hwnd, GWLP_WNDPROC, (LONG_PTR)chrome_wndproc);
            }
        } // namespace

        void chrome_platform_init(App *app)
        {
            s_app = app;

            HWND hwnd = (HWND)gfx::native_window_handle();
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

            // Install the subclass BEFORE SetWindowPos so the WM_NCCALCSIZE
            // triggered by SWP_FRAMECHANGED is handled by our proc (returns
            // 0 → no non-client area).
            ensure_subclass();

            SetWindowPos(hwnd, NULL, wr.left, wr.top, cw, ch,
                         SWP_NOZORDER | SWP_FRAMECHANGED);

            // Re-assert foreground + focus — switching to WS_POPUP can cost
            // the window keyboard focus on some systems.
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
        }

        void chrome_platform_frame(int drag_right, bool drag_enabled)
        {
            s_drag_right = drag_right;
            s_drag_enabled = drag_enabled;

            // Allegro can recreate its window, so re-assert the subclass.
            ensure_subclass();
        }

        void chrome_platform_begin_drag()
        {
            if (!s_drag_enabled)
                return;

            HWND hwnd = (HWND)gfx::native_window_handle();
            if (!hwnd)
                return;

            ReleaseCapture();
            SendMessage(hwnd, WM_NCLBUTTONDOWN, HTCAPTION, 0);
        }

        void chrome_platform_minimize()
        {
            HWND hwnd = (HWND)gfx::native_window_handle();
            if (hwnd)
                ShowWindow(hwnd, SW_MINIMIZE);
        }

        void chrome_platform_maximize_toggle()
        {
            HWND hwnd = (HWND)gfx::native_window_handle();
            if (!hwnd)
                return;

            ShowWindow(hwnd, IsZoomed(hwnd) ? SW_RESTORE : SW_MAXIMIZE);
        }

    } // namespace ui
} // namespace launcher
