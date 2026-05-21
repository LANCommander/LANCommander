#ifndef LAUNCHER_UI_WINDOW_CHROME_H
#define LAUNCHER_UI_WINDOW_CHROME_H

#include "input.h"

struct BITMAP;

namespace launcher
{

    class App;

    namespace ui
    {

        // Height of the custom title bar / footer in pixels.
        int chrome_height();
        int footer_height();

        // Remove the native Windows frame from the Allegro window.
        void chrome_remove_frame();

        // Draw the title bar (semi-transparent overlay) and handle drag / close / minimize.
        // Returns true if the close button was clicked.
        bool window_chrome_draw(App &app, const InputState &input);

        // Draw the footer bar (Depot/Library toggle, download status).
        void window_footer_draw(App &app, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WINDOW_CHROME_H
