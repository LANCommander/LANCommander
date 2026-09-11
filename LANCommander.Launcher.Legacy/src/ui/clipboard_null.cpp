// clipboard_null.cpp — a clipboard that never leaves the process.
//
// Linked by launcher_tests so copy and paste can be exercised without a
// display server, and available to any future backend (DOS) that has no
// system clipboard to talk to. Behaves like a real one for a single process.

#include "ui/clipboard.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            std::string &store()
            {
                static std::string s;
                return s;
            }
        } // namespace

        bool clipboard_get(std::string *out)
        {
            if (!out || store().empty())
                return false;
            *out = store();
            return true;
        }

        bool clipboard_set(const char *utf8)
        {
            if (!utf8)
                return false;
            store() = utf8;
            return true;
        }

    } // namespace ui
} // namespace launcher
