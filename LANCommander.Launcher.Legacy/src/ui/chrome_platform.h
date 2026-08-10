#ifndef LAUNCHER_UI_CHROME_PLATFORM_H
#define LAUNCHER_UI_CHROME_PLATFORM_H

// Platform half of the custom window chrome.
//
// The launcher draws its own title bar and footer over a frameless window.
// Drawing is portable and lives in window_chrome.cpp; making the window
// frameless, hit-testing its resize edges and dragging it by the title bar
// are not, and live here.
//
// One implementation per graphics backend:
//   chrome_platform_sdl.cpp   — SDL_SetWindowHitTest + SDL_MinimizeWindow
//   chrome_platform_win32.cpp — WndProc subclass (transitional, Allegro only)
//
// A DOS build would supply a stub: fullscreen, nothing to hit-test.

namespace launcher
{
    class App;

    namespace ui
    {

        // Chrome geometry. Shared by the drawing side and the hit test so the
        // two can never disagree about where the resize border is.
        struct ChromeMetrics
        {
            int chrome_h;      // title bar height
            int footer_h;
            int resize_border; // grab width of the resize edges
            int min_w, min_h;
        };

        const ChromeMetrics &chrome_metrics();

        // Called once after the display exists. Strips the native frame if
        // the backend has not already done so, and installs whatever hit
        // testing the platform needs.
        void chrome_platform_init(App *app);

        // Called once per frame by window_chrome_draw.
        //
        // `drag_right` is the x past which the title bar stops being a drag
        // handle (the close/minimize/user buttons live to its right).
        // `drag_enabled` is false while the user dropdown is open, so a click
        // meant to dismiss it is not swallowed by a window drag.
        //
        // Under SDL the hit test reads these one frame late, which is fine —
        // the old SendMessage(WM_NCLBUTTONDOWN) path was equally a frame
        // behind.
        void chrome_platform_frame(int drag_right, bool drag_enabled);

        // Start dragging the window from the title bar. A no-op where the
        // platform already handles dragging via the hit test.
        void chrome_platform_begin_drag();

        void chrome_platform_minimize();

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_CHROME_PLATFORM_H
