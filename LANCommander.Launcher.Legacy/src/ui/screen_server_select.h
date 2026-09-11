#ifndef LAUNCHER_UI_SCREEN_SERVER_SELECT_H
#define LAUNCHER_UI_SCREEN_SERVER_SELECT_H

#include "ui/input.h"

namespace launcher
{
    class App;

    namespace ui
    {

        // Step one of signing in: choose which server to talk to, either by
        // typing an address or by picking one the UDP beacon found.
        void screen_server_select_draw(App &app, const InputState &input);

        // Re-seed the address box from settings and clear any probe in
        // progress. Called when the user comes back here from the credentials
        // screen via Change.
        void screen_server_select_reset();

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_SERVER_SELECT_H
