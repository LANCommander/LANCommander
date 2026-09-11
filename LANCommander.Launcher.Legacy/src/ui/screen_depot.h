#ifndef LAUNCHER_UI_SCREEN_DEPOT_H
#define LAUNCHER_UI_SCREEN_DEPOT_H

#include "ui/input.h"

namespace launcher
{
    class App;

    namespace ui
    {

        void screen_depot_draw(App &app, const InputState &input);

        // Drop the derived section indices so the next draw rebuilds them.
        void screen_depot_reset();

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_DEPOT_H
