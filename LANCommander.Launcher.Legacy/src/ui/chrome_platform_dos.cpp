// chrome_platform_dos.cpp — the stub chrome_platform.h describes for DOS.
//
// The launcher owns the whole screen: there is no frame to strip, no window
// to drag, nothing to minimise to, and no hit test for a window manager to
// consult. Everything the chrome draws — the title bar, the footer, the close
// button — still works, because that half is portable and lives in
// window_chrome.cpp. Only the parts that need a window manager are absent.
//
// Closing is not stubbed out anywhere: the chrome's X button goes through
// App, not through here.

#include "ui/chrome_platform.h"

namespace launcher
{
    namespace ui
    {

        void chrome_platform_init(App *app) { (void)app; }

        void chrome_platform_frame(int drag_right, bool drag_enabled)
        {
            (void)drag_right;
            (void)drag_enabled;
        }

        void chrome_platform_begin_drag() {}

        // A DOS program has nowhere to minimise to, and it already owns the
        // whole screen so there is nothing to maximise into either. Left as
        // no-ops rather than hidden from the chrome, so the buttons' presence
        // stays a question about the UI rather than one about the platform.
        void chrome_platform_minimize() {}

        void chrome_platform_maximize_toggle() {}

    } // namespace ui
} // namespace launcher
