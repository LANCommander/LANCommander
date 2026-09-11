#include "app/game_menu.h"

#include <cstddef>

namespace launcher
{

    namespace
    {
        struct Candidate
        {
            const char *label;
            int command;
            bool visible;
            bool enabled;
        };
    } // namespace

    int build_game_menu(const GameMenuFlags &f, GameMenuEntry *out, int cap)
    {
        if (!out || cap <= 0)
            return 0;

        // Room for the fixed entries plus a bounded number of dynamic ones.
        const int MAX_SECONDARY = 16;
        int sec = f.secondary_count;
        if (sec < 0) sec = 0;
        if (sec > MAX_SECONDARY) sec = MAX_SECONDARY;

        Candidate items[32];
        int n = 0;

        items[n].label = "Install";
        items[n].command = GM_Install;
        items[n].visible = !f.installed;
        items[n].enabled = !f.offline;
        ++n;

        items[n].label = "Play";
        items[n].command = GM_Play;
        items[n].visible = f.installed;
        items[n].enabled = true;
        ++n;

        items[n].label = "Update";
        items[n].command = GM_Update;
        items[n].visible = f.installed && f.update_available && !f.running;
        items[n].enabled = !f.offline;
        ++n;

        items[n].label = "Play Without Updating";
        items[n].command = GM_PlayNoUpdate;
        items[n].visible = f.installed && f.update_available;
        items[n].enabled = true;
        ++n;

        // Dynamic secondary actions sit here, between the play verbs and the
        // file operations, as they do in the Avalonia menu.
        for (int i = 0; i < sec; ++i)
        {
            items[n].label = NULL; // filled in by the caller, which has names
            items[n].command = GM_SecondaryBase + i;
            items[n].visible = f.installed;
            items[n].enabled = true;
            ++n;
        }

        items[n].label = NULL;
        items[n].command = GM_Separator;
        // Bound to `installed`, matching Separator(vm, "IsInstalled") in the
        // Avalonia menu. The group these divide is the file operations, so
        // with nothing installed there is nothing to divide and an
        // unconditional separator would sit between Install and Add to
        // Library for no reason.
        items[n].visible = f.installed;
        items[n].enabled = true;
        ++n;

        items[n].label = "Browse Files";
        items[n].command = GM_BrowseFiles;
        items[n].visible = f.installed;
        items[n].enabled = true;
        ++n;

        items[n].label = "View Manual";
        items[n].command = GM_ViewManual;
        items[n].visible = f.installed && f.has_manuals;
        items[n].enabled = true;
        ++n;

        items[n].label = "Modify";
        items[n].command = GM_Modify;
        items[n].visible = f.installed;
        items[n].enabled = !f.offline;
        ++n;

        items[n].label = NULL;
        items[n].command = GM_Separator;
        // Bound to `installed`, matching Separator(vm, "IsInstalled") in the
        // Avalonia menu. The group these divide is the file operations, so
        // with nothing installed there is nothing to divide and an
        // unconditional separator would sit between Install and Add to
        // Library for no reason.
        items[n].visible = f.installed;
        items[n].enabled = true;
        ++n;

        items[n].label = "Verify Files";
        items[n].command = GM_VerifyFiles;
        items[n].visible = f.installed;
        items[n].enabled = !f.verifying;
        ++n;

        items[n].label = "Uninstall";
        items[n].command = GM_Uninstall;
        items[n].visible = f.installed;
        items[n].enabled = !f.uninstalling;
        ++n;

        items[n].label = "Add to Library";
        items[n].command = GM_AddToLibrary;
        items[n].visible = !f.in_library;
        items[n].enabled = !f.offline;
        ++n;

        items[n].label = "Remove from Library";
        items[n].command = GM_RemoveFromLibrary;
        items[n].visible = f.in_library;
        items[n].enabled = !f.offline;
        ++n;

        // Emit, collapsing separators.
        //
        // A separator is held back until a real entry follows it, which drops
        // leading and doubled ones; one still pending at the end would be
        // trailing, so it is simply never written. This is the part that goes
        // wrong when the table is expanded inline at the call site.
        int written = 0;
        bool pending_sep = false;

        for (int i = 0; i < n; ++i)
        {
            if (!items[i].visible)
                continue;

            if (items[i].command == GM_Separator)
            {
                if (written > 0)
                    pending_sep = true;
                continue;
            }

            if (pending_sep)
            {
                pending_sep = false;
                if (written >= cap)
                    break;
                out[written].label = NULL;
                out[written].command = GM_Separator;
                out[written].enabled = false;
                ++written;
            }

            if (written >= cap)
                break;

            out[written].label = items[i].label;
            out[written].command = items[i].command;
            out[written].enabled = items[i].enabled;
            ++written;
        }

        return written;
    }

    const char *game_primary_label(const GameMenuFlags &f, bool starting,
                                   bool stopping, bool installing)
    {
        if (!f.installed)
            return installing ? "Installing..." : "Install";

        if (stopping)
            return "Stopping";
        if (f.running)
            return "Stop";
        if (starting)
            return "Starting...";
        if (f.update_available)
            return "Update";

        return "Play";
    }

    int game_primary_command(const GameMenuFlags &f)
    {
        if (!f.installed)
            return GM_Install;
        if (f.running)
            return GM_Play; // the running state turns Play into Stop
        if (f.update_available)
            return GM_Update;
        return GM_Play;
    }

} // namespace launcher
