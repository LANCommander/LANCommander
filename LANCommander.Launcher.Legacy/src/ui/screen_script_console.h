#ifndef LAUNCHER_UI_SCREEN_SCRIPT_CONSOLE_H
#define LAUNCHER_UI_SCREEN_SCRIPT_CONSOLE_H

#include "input.h"

// Everything the user sees about scripts.
//
// Three surfaces, because they answer three different questions and only one
// of them is a screen:
//
//   screen_script_console_draw  "what did my Install.ps1 print, and why did
//                                it fail?" -- the full history, the source,
//                                and where the breakpoints are.
//
//   script_tail_draw            "is anything happening right now?" -- a small
//                                strip over whatever screen is up, so output
//                                is visible during an install without leaving
//                                the library.
//
//   script_debugger_draw        the whole window, drawn by App::pump_debug_frame
//                                while a script is paused on the drawing
//                                thread. Nothing else can advance at that
//                                point, so nothing else is drawn.

namespace launcher
{

    class App;

    namespace ui
    {

        void screen_script_console_draw(App &app, const InputState &input);

        // Draws nothing unless a script is running, one has just finished, or
        // the user pinned the strip open. Safe to call every frame.
        void script_tail_draw(App &app, const InputState &input);

        void script_debugger_draw(App &app, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_SCRIPT_CONSOLE_H
