// clipboard_sdl.cpp — system clipboard on SDL3.
//
// SDL already normalises the platform clipboard to UTF-8, so this is close to
// a direct pass-through. The only thing worth care is ownership: the string
// SDL hands back is heap-allocated and must go through SDL_free, not delete.

#include "ui/clipboard.h"

#include <SDL3/SDL.h>

namespace launcher
{
    namespace ui
    {

        bool clipboard_get(std::string *out)
        {
            if (!out)
                return false;

            if (!SDL_HasClipboardText())
                return false;

            char *text = SDL_GetClipboardText();
            if (!text)
                return false;

            // SDL returns an empty string rather than NULL when it has nothing
            // useful, which is not worth reporting as success.
            if (text[0] == '\0')
            {
                SDL_free(text);
                return false;
            }

            *out = text;
            SDL_free(text);
            return true;
        }

        bool clipboard_set(const char *utf8)
        {
            if (!utf8)
                return false;
            return SDL_SetClipboardText(utf8);
        }

    } // namespace ui
} // namespace launcher
