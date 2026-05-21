#ifndef LAUNCHER_UI_WIDGETS_H
#define LAUNCHER_UI_WIDGETS_H

#include <string>

#include "input.h"

struct BITMAP;

namespace launcher
{
    namespace ui
    {

        // --- Button ---

        struct ButtonState
        {
            bool hovered;
            bool clicked; // true on the frame the mouse was released over the button
        };

        // Draw a button and return its interaction state.
        ButtonState button(BITMAP *bmp, int x, int y, int w, int h, const char *label,
                           const InputState &input);

        // --- Text Input ---

        struct TextInputState
        {
            bool focused;
            bool submitted; // true when Enter was pressed while focused
        };

        // Draw a text input field. `buffer` is modified in-place.
        // `max_len` is the maximum number of characters.
        // `password` replaces characters with asterisks when true.
        TextInputState text_input(BITMAP *bmp, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  bool password = false);

        // --- Label ---

        void label(BITMAP *bmp, int x, int y, int color, const char *text);

        // --- Panel ---

        void panel(BITMAP *bmp, int x, int y, int w, int h, int color);

        // --- Divider ---

        void divider(BITMAP *bmp, int x, int y, int w);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WIDGETS_H
