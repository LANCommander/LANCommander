#ifndef LAUNCHER_UI_CHROME_GEOMETRY_H
#define LAUNCHER_UI_CHROME_GEOMETRY_H

// Where a point falls on the custom window chrome.
//
// Deliberately free of any platform or graphics dependency: this is the one
// piece of the chrome that has to agree between backends, and it used to be
// duplicated once per platform (WM_NCHITTEST in chrome_platform_win32.cpp and
// the SDL_SetWindowHitTest callback in chrome_platform_sdl.cpp). Keeping the
// arithmetic in one place means the two cannot drift, and lets it be tested
// without a window.

namespace launcher
{
    namespace ui
    {

        enum class ChromeHit
        {
            Client,     // ordinary content; the UI handles it
            Draggable,  // title bar — moves the window

            ResizeTop,
            ResizeBottom,
            ResizeLeft,
            ResizeRight,
            ResizeTopLeft,
            ResizeTopRight,
            ResizeBottomLeft,
            ResizeBottomRight
        };

        // `x`/`y` are client-relative. `drag_right` is the x past which the
        // title bar stops being a drag handle, because the close/minimise/user
        // buttons live there. `drag_enabled` is false while the user dropdown
        // is open, so the click that should dismiss it is not swallowed by a
        // window drag.
        //
        // Corners win over edges, and edges win over the title bar: grabbing
        // the top-left pixel should resize, not drag.
        ChromeHit chrome_hit_test(int x, int y, int win_w, int win_h,
                                  int chrome_h, int resize_border,
                                  int drag_right, bool drag_enabled);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_CHROME_GEOMETRY_H
