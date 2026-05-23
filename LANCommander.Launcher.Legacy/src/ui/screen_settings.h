#ifndef LAUNCHER_UI_SCREEN_SETTINGS_H
#define LAUNCHER_UI_SCREEN_SETTINGS_H

#include "input.h"

namespace launcher
{

    class App;

    namespace ui
    {

        void screen_settings_draw(App &app, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_SETTINGS_H
