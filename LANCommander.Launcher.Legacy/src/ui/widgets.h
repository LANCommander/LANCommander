#ifndef LAUNCHER_UI_WIDGETS_H
#define LAUNCHER_UI_WIDGETS_H

#include <string>

#include "gfx/gfx.h"
#include "input.h"

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
        ButtonState button(gfx::Surface *s, int x, int y, int w, int h, const char *label,
                           const InputState &input);

        // --- Text Input ---

        struct TextInputState
        {
            bool focused;
            bool submitted; // true when Enter was pressed while focused
        };

        // Draw a text input field. `buffer` is modified in-place.
        // `max_len` is the maximum length in bytes.
        // `password` replaces characters with asterisks when true.
        TextInputState text_input(gfx::Surface *s, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  bool password = false);

        // --- Label ---

        void label(gfx::Surface *s, int x, int y, gfx::Color color, const char *text);

        // --- Panel ---

        void panel(gfx::Surface *s, int x, int y, int w, int h, gfx::Color color);

        // --- Divider ---

        void divider(gfx::Surface *s, int x, int y, int w);

        // --- Scrollbar ---

        // Draw a vertical scrollbar track + thumb with click/drag support.
        // `scroll_y` is updated in-place when the user drags the thumb
        // or clicks the track.
        void scrollbar(gfx::Surface *s, int x, int y, int h,
                       int content_h, int viewport_h, int &scroll_y,
                       const InputState &input);

        // --- Modal overlay ---

        // Draw a semi-transparent dark backdrop over the entire screen.
        void modal_backdrop(gfx::Surface *s, int sw, int sh);

        // --- Checkbox ---

        // Draw a checkbox with label. Returns true if toggled this frame.
        bool checkbox(gfx::Surface *s, int x, int y, const char *label_text,
                      bool &checked, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WIDGETS_H
