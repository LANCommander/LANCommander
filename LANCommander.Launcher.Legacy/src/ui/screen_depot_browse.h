#ifndef LAUNCHER_UI_SCREEN_DEPOT_BROWSE_H
#define LAUNCHER_UI_SCREEN_DEPOT_BROWSE_H

#include "ui/input.h"

namespace launcher
{
    class App;

    namespace ui
    {

        // The filtered grid a See-all, a genre tile, a collection tile or a
        // search lands on. Reads its filter from App.
        void screen_depot_browse_draw(App &app, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_DEPOT_BROWSE_H
