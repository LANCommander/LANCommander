// chrome_platform_sdl.cpp — frameless-window chrome on SDL3.
//
// SDL creates the window borderless and resizable already, so almost all of
// the Win32 version disappears: no style stripping, no WndProc subclass, no
// WM_NCCALCSIZE / WM_ERASEBKGND / WM_PAINT handling, no manual cursor
// selection, no DWM opt-out.
//
// What remains is the hit test. SDL_SetWindowHitTest is a near-exact match
// for WM_NCHITTEST: return SDL_HITTEST_RESIZE_* for the edges and
// SDL_HITTEST_DRAGGABLE for the title bar, and SDL handles the resize and
// drag loops (and the resize cursors) itself.

#include "ui/chrome_platform.h"
#include "ui/chrome_geometry.h"

#include <SDL3/SDL.h>

namespace launcher
{
    namespace gfx
    {
        SDL_Window *display_window(); // gfx_sdl.cpp
    }

    namespace ui
    {

        namespace
        {
            int s_drag_right = 0;
            bool s_drag_enabled = true;

            // The arithmetic lives in chrome_geometry.cpp so this and the
            // Win32 WM_NCHITTEST handler cannot disagree about where the
            // resize border is; all that happens here is the mapping onto
            // SDL's enum.
            SDL_HitTestResult SDLCALL hit_test(SDL_Window *win,
                                               const SDL_Point *pt,
                                               void *data)
            {
                (void)data;

                const ChromeMetrics &m = chrome_metrics();

                int w = 0, h = 0;
                SDL_GetWindowSize(win, &w, &h);

                switch (chrome_hit_test(pt->x, pt->y, w, h,
                                        m.chrome_h, m.resize_border,
                                        s_drag_right, s_drag_enabled))
                {
                case ChromeHit::ResizeTopLeft:     return SDL_HITTEST_RESIZE_TOPLEFT;
                case ChromeHit::ResizeTopRight:    return SDL_HITTEST_RESIZE_TOPRIGHT;
                case ChromeHit::ResizeBottomLeft:  return SDL_HITTEST_RESIZE_BOTTOMLEFT;
                case ChromeHit::ResizeBottomRight: return SDL_HITTEST_RESIZE_BOTTOMRIGHT;
                case ChromeHit::ResizeTop:         return SDL_HITTEST_RESIZE_TOP;
                case ChromeHit::ResizeBottom:      return SDL_HITTEST_RESIZE_BOTTOM;
                case ChromeHit::ResizeLeft:        return SDL_HITTEST_RESIZE_LEFT;
                case ChromeHit::ResizeRight:       return SDL_HITTEST_RESIZE_RIGHT;
                case ChromeHit::Draggable:         return SDL_HITTEST_DRAGGABLE;
                default:                           return SDL_HITTEST_NORMAL;
                }
            }
        } // namespace

        void chrome_platform_init(App *app)
        {
            (void)app; // resize arrives as an SDL event, not a callback

            SDL_Window *win = gfx::display_window();
            if (!win)
                return;

            const ChromeMetrics &m = chrome_metrics();
            SDL_SetWindowMinimumSize(win, m.min_w, m.min_h);
            SDL_SetWindowHitTest(win, hit_test, NULL);
        }

        void chrome_platform_frame(int drag_right, bool drag_enabled)
        {
            s_drag_right = drag_right;
            s_drag_enabled = drag_enabled;
        }

        void chrome_platform_begin_drag()
        {
            // Nothing to do: SDL_HITTEST_DRAGGABLE already runs the drag loop.
        }

        void chrome_platform_minimize()
        {
            SDL_Window *win = gfx::display_window();
            if (win)
                SDL_MinimizeWindow(win);
        }

        void chrome_platform_maximize_toggle()
        {
            SDL_Window *win = gfx::display_window();
            if (!win)
                return;

            // The resulting size change arrives as an ordinary SDL window
            // event, which App already handles — nothing here has to tell the
            // renderer the backbuffer grew.
            if (SDL_GetWindowFlags(win) & SDL_WINDOW_MAXIMIZED)
                SDL_RestoreWindow(win);
            else
                SDL_MaximizeWindow(win);
        }

    } // namespace ui
} // namespace launcher
