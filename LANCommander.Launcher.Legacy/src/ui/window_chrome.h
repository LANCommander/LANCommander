#ifndef LAUNCHER_UI_WINDOW_CHROME_H
#define LAUNCHER_UI_WINDOW_CHROME_H

#include "gfx/gfx.h"
#include "input.h"

namespace launcher
{

    class App;

    namespace ui
    {

        // Height of the custom title bar / footer in pixels.
        int chrome_height();

        int footer_height();

        // True when window_footer_draw() will run for the screen App is on.
        // App::run() and chrome_gate() must agree about this, or the gate
        // blocks a strip of a screen that has no footer over it.
        bool footer_visible(App &app);

        // The input a SCREEN should be drawn with.
        //
        // The title bar, the footer and the profile dropdown are drawn AFTER
        // the screen but sit on top of it, so an unmodified input let a click
        // land on both: the screen hit-tested the pointer first and had no
        // idea the chrome would later cover it. Clicking the profile button
        // over a scrolled library grid opened the menu and selected the game
        // underneath, in the same frame.
        //
        // This returns `input` with the pointer suppressed wherever the chrome
        // is about to draw. The chrome itself is still given the raw input.
        InputState chrome_gate(App &app, const InputState &input);

        // Draw the title bar (semi-transparent overlay) and handle drag / close / minimize.
        // Returns true if the close button was clicked.
        bool window_chrome_draw(App &app, const InputState &input);

        // Draw the footer bar: the Library/Depot switch on the left, download
        // status in the middle. Must be drawn on every screen that reserves
        // footer_height(), which is every screen behind the shell.
        void window_footer_draw(App &app, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WINDOW_CHROME_H
