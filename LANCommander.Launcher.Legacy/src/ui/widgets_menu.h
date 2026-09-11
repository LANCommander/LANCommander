#ifndef LAUNCHER_UI_WIDGETS_MENU_H
#define LAUNCHER_UI_WIDGETS_MENU_H

#include "gfx/gfx.h"
#include "input.h"

// Split buttons and dropdown menus.
//
// Layout arithmetic (edge flips, row hit rects) is in layout.cpp and tested;
// what lives here is drawing and input.

namespace launcher
{
    namespace ui
    {

        // --- Split button ------------------------------------------------------

        struct SplitButtonResult
        {
            bool primary_clicked;
            bool caret_clicked;
            bool hovered;

            SplitButtonResult()
                : primary_clicked(false), caret_clicked(false), hovered(false) {}
        };

        // A primary action with a caret on the right that opens a menu.
        // `bg`/`bg_hover` are explicit because the Play button turns red while
        // a game is running.
        SplitButtonResult split_button(gfx::Surface *s, int x, int y, int w, int h,
                                       const char *label, bool enabled,
                                       gfx::Color bg, gfx::Color bg_hover,
                                       const InputState &input);

        // --- Context menu ------------------------------------------------------

        struct MenuItemDef
        {
            const char *label;  // NULL draws a separator
            int command;
            bool enabled;
        };

        struct MenuState
        {
            bool open;
            int anchor_x, anchor_y;

            // Set by menu_open(), consumed by context_menu().
            //
            // Immediate mode draws the menu in the SAME frame as the click
            // that opened it, and context_menu() closes on any click so that
            // clicking away dismisses it. Without this the opening click was
            // still `clicked` when the menu ran, so every menu opened and shut
            // inside one frame and read as a flash.
            bool opened_this_frame;

            MenuState()
                : open(false), anchor_x(0), anchor_y(0),
                  opened_this_frame(false) {}
        };

        // Open `state` anchored at (x, y), suppressing the click that did it
        // so context_menu() does not immediately close again. Always prefer
        // this to setting `open` by hand.
        void menu_open(MenuState &state, int x, int y);

        // Draws the menu when open and returns the command of the item clicked
        // this frame, or 0.
        //
        // Closes itself on a click (inside or outside) and on Escape. The
        // caller is responsible for telling App an overlay is active while
        // `state.open`, so the global Escape handler stands down.
        int context_menu(gfx::Surface *s, int screen_w, int screen_h,
                         const MenuItemDef *items, int count,
                         MenuState &state, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WIDGETS_MENU_H
