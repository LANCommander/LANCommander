#ifndef LAUNCHER_UI_CLIPBOARD_H
#define LAUNCHER_UI_CLIPBOARD_H

#include <string>

// System clipboard access. One implementation per graphics backend, following
// the same split as font_ttf/font_gdi and input_sdl/input_win32:
//
//   clipboard_sdl.cpp   — SDL_GetClipboardText / SDL_SetClipboardText
//   clipboard_win32.cpp — OpenClipboard / GetClipboardData (Allegro build)
//   clipboard_null.cpp  — a process-local string, for tests and for DOS
//
// Text is UTF-8 at this boundary regardless of what the platform stores.

namespace launcher
{
    namespace ui
    {

        // Returns false when the clipboard is empty, unavailable, or holds
        // something that is not text. `out` is untouched on failure.
        bool clipboard_get(std::string *out);

        bool clipboard_set(const char *utf8);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_CLIPBOARD_H
