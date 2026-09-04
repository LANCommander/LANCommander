#ifndef LAUNCHER_UI_WIDGETS_H
#define LAUNCHER_UI_WIDGETS_H

#include <string>

#include "gfx/gfx.h"
#include "icons.h"
#include "input.h"
#include "text_edit.h"

namespace launcher
{
    namespace ui
    {

        // --- Overlay input gating ---

        // Returns a copy of `in` with the pointer moved off-screen and every
        // button, key and text event stripped.
        //
        // Immediate mode draws an overlay last but has to hit-test it first,
        // which a single pass cannot do. Rather than teach every screen to
        // guard each widget, draw the content underneath an open overlay
        // against a neutered input: nothing below can hover, click or receive
        // a keystroke, which is exactly what "modal" means.
        InputState input_blocked(const InputState &in);

        // Returns a copy of `in` with only the POINTER suppressed: moved
        // off-screen, buttons, wheel, click and press cleared. Keys and text
        // survive.
        //
        // The difference from input_blocked() matters. A modal owns the whole
        // frame, so it takes the keyboard too. A bar drawn ON TOP of a screen
        // owns only the pixels it covers: typing in a search box must keep
        // working while the pointer happens to be resting over the footer.
        InputState input_without_pointer(const InputState &in);

        // --- Rounded rectangles ---

        // Filled rectangle with rounded corners, built from horizontal spans
        // so it needs nothing from the gfx backend beyond fill_rect.
        //
        // No antialiasing: at the 3-4px radii the UI uses, on a fixed-DPI
        // software surface, a stepped corner is indistinguishable from a
        // smooth one, and an AA'd edge would need to read the destination —
        // which is the one thing a plain fill does not have to do.
        void fill_rounded_rect(gfx::Surface *s, const gfx::Rect &r, int radius,
                               gfx::Color c);

        // Same, alpha-blended using c.a.
        void fill_rounded_rect_alpha(gfx::Surface *s, const gfx::Rect &r, int radius,
                                     gfx::Color c);

        // One-pixel rounded outline, drawn inside `r`.
        void draw_rounded_rect(gfx::Surface *s, const gfx::Rect &r, int radius,
                               gfx::Color c);

        // Paints the corner pixels that fall OUTSIDE a rounded rect with `bg`,
        // rounding artwork that has already been drawn into `r`.
        //
        // A real rounded clip would need the blitter to mask per pixel, which
        // gfx does not do. Overpainting works because the only things that get
        // rounded here sit on a solid, known background -- the avatar on the
        // profile button, for one.
        void round_rect_corners(gfx::Surface *s, const gfx::Rect &r, int radius,
                                gfx::Color bg);

        // The corner radius and padding the UI is built around. Buttons are
        // sized from these rather than from literals scattered per screen.
        extern const int BUTTON_RADIUS;
        extern const int BUTTON_PAD_X;
        extern const int BUTTON_PAD_Y;

        // --- Button ---

        struct ButtonState
        {
            bool hovered;
            bool clicked; // true on the frame the mouse was released over the button
        };

        // Draw a button and return its interaction state.
        ButtonState button(gfx::Surface *s, int x, int y, int w, int h, const char *label,
                           const InputState &input);

        // Button with a leading icon. `icon` may be Icon::None, in which case
        // this is exactly button().
        ButtonState icon_button(gfx::Surface *s, int x, int y, int w, int h,
                                Icon icon, const char *label,
                                const InputState &input);

        // Width a button needs for `label` (and optionally an icon) at the
        // standard padding. Lets callers lay a row of buttons out without
        // each one hard-coding the same arithmetic.
        int button_width(const char *label, Icon icon = Icon::None);

        // Standard button height: text plus vertical padding.
        int button_height();

        // --- Badge ---

        // A rounded capsule with a label, as the Avalonia Button.Badge style
        // draws genres, tags, platforms and the like. Returns hover/click so a
        // badge can navigate.
        //
        // `w` is derived from the label; use badge_width() to lay out a wrap.
        ButtonState badge(gfx::Surface *s, int x, int y, const char *label,
                          bool interactive, const InputState &input);

        int badge_width(const char *label);
        int badge_height();

        // --- Text Input ---

        struct TextInputState
        {
            bool focused;
            bool submitted; // true when Enter was pressed while focused
        };

        // Draw a text input field. `buffer` is modified in-place.
        // `max_len` is the maximum length in bytes.
        // `password` replaces characters with asterisks when true.
        //
        // `edit` carries the caret, selection and horizontal scroll between
        // frames, and belongs to the caller for the same reason ScrollState
        // does: two fields on one screen must not share a caret.
        TextInputState text_input(gfx::Surface *s, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  TextEditState &edit,
                                  bool password = false);

        // --- Label ---

        void label(gfx::Surface *s, int x, int y, gfx::Color color, const char *text);

        // --- Panel ---

        void panel(gfx::Surface *s, int x, int y, int w, int h, gfx::Color color);

        // --- Divider ---

        void divider(gfx::Surface *s, int x, int y, int w);

        // --- Scrollbar ---

        // Everything a scrollbar remembers between frames.
        //
        // This used to be two file-static globals in widgets.cpp, which meant
        // every scrollbar in the program shared one drag. That is reachable
        // today: the game detail page draws its own scrollbar and then the
        // add-on list inside the install dialog draws a second one over it, so
        // dragging either moved both. State belongs to the caller.
        struct ScrollState
        {
            int offset;      // pixels scrolled from the top
            bool dragging;   // thumb drag in progress
            int drag_offset; // mouse offset from the thumb top when it began

            ScrollState() : offset(0), dragging(false), drag_offset(0) {}
        };

        // Draw a vertical scrollbar track + thumb with click/drag support.
        // `state.offset` is updated in-place when the user drags the thumb
        // or clicks the track.
        void scrollbar(gfx::Surface *s, int x, int y, int h,
                       int content_h, int viewport_h, ScrollState &state,
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
