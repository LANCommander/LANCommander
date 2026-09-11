#ifndef LAUNCHER_UI_SCREEN_GAME_DETAIL_H
#define LAUNCHER_UI_SCREEN_GAME_DETAIL_H

#include <string>

#include "input.h"

namespace launcher
{

    class App;

    namespace ui
    {

        void screen_game_detail_draw(App &app, const InputState &input);

        // Title of the game currently on the detail page, or an empty string
        // while it is still loading. The title bar shows it, the way the
        // Avalonia shell's ContentViewTitle reads GameDetailViewModel.Title.
        const std::string &screen_game_detail_title();

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_SCREEN_GAME_DETAIL_H
