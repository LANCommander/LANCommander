// clipboard_win32.cpp — system clipboard on plain Win32 (Allegro build).
//
// Uses CF_TEXT, not CF_UNICODETEXT. Windows 95 and 98 implement the Unicode
// clipboard formats only partially, and the whole point of the Allegro backend
// is that it targets those. The cost is real and worth stating plainly:
//
//   CF_TEXT is ANSI in the active code page, so a UTF-8 string containing
//   anything outside ASCII does not survive a round trip through the system
//   clipboard on this backend. ASCII, which covers server addresses,
//   usernames and paths, is unaffected.
//
// The SDL backend has no such limitation.

#include "ui/clipboard.h"

#include <windows.h>

namespace launcher
{
    namespace ui
    {

        bool clipboard_get(std::string *out)
        {
            if (!out)
                return false;

            if (!IsClipboardFormatAvailable(CF_TEXT))
                return false;

            if (!OpenClipboard(NULL))
                return false;

            bool ok = false;
            HANDLE h = GetClipboardData(CF_TEXT);
            if (h)
            {
                const char *text = (const char *)GlobalLock(h);
                if (text)
                {
                    if (text[0] != '\0')
                    {
                        *out = text;
                        ok = true;
                    }
                    GlobalUnlock(h);
                }
            }

            CloseClipboard();
            return ok;
        }

        bool clipboard_set(const char *utf8)
        {
            if (!utf8)
                return false;

            const size_t len = strlen(utf8);

            // The clipboard takes ownership of this block on success, so it is
            // deliberately not freed here. It is freed on every failure path
            // below, because until SetClipboardData succeeds it is still ours.
            HGLOBAL mem = GlobalAlloc(GMEM_MOVEABLE, len + 1);
            if (!mem)
                return false;

            char *dst = (char *)GlobalLock(mem);
            if (!dst)
            {
                GlobalFree(mem);
                return false;
            }
            memcpy(dst, utf8, len + 1);
            GlobalUnlock(mem);

            if (!OpenClipboard(NULL))
            {
                GlobalFree(mem);
                return false;
            }

            EmptyClipboard();
            if (!SetClipboardData(CF_TEXT, mem))
            {
                GlobalFree(mem);
                CloseClipboard();
                return false;
            }

            CloseClipboard();
            return true;
        }

    } // namespace ui
} // namespace launcher
