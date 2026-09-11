#ifndef LAUNCHER_APP_GAME_MENU_H
#define LAUNCHER_APP_GAME_MENU_H

// The game context menu, as data.
//
// One table drives the dropdown on the game detail page, and will drive the
// right-click menu on covers and list rows when those arrive. Building it as
// a pure function of a flags struct is what makes the awkward part — which
// entries are visible in which state, and where the separators land once the
// invisible ones are gone — checkable without a window.
//
// Deliberately free of gfx, SDK and Win32 so it compiles into launcher_tests.

namespace launcher
{

    // Commands the menu can emit. Values are stable so a caller can switch on
    // them; 0 means "nothing was chosen this frame".
    enum GameMenuCmd
    {
        GM_None = 0,
        GM_Install,
        GM_Play,
        GM_Update,
        GM_PlayNoUpdate,
        GM_BrowseFiles,
        GM_ViewManual,
        GM_Modify,
        GM_VerifyFiles,
        GM_Uninstall,
        GM_AddToLibrary,
        GM_RemoveFromLibrary,

        // A separator, which carries no command.
        GM_Separator,

        // Secondary actions are dynamic, so they occupy a range rather than a
        // single value: GM_SecondaryBase + index into the action list.
        GM_SecondaryBase = 1000
    };

    struct GameMenuFlags
    {
        bool installed;
        bool in_library;
        bool update_available;
        bool running;
        bool has_manuals;
        bool verifying;
        bool uninstalling;
        bool offline;

        // Number of non-primary actions to offer inline.
        int secondary_count;

        GameMenuFlags()
            : installed(false), in_library(false), update_available(false),
              running(false), has_manuals(false), verifying(false),
              uninstalling(false), offline(false), secondary_count(0) {}
    };

    struct GameMenuEntry
    {
        const char *label;  // NULL for a separator
        int command;
        bool enabled;
    };

    // Fills `out` and returns how many entries were written.
    //
    // Order matches GameContextMenu.Populate in the Avalonia launcher:
    // Install / Play / Update / Play Without Updating / secondary actions /
    // separator / Browse Files / View Manual / Modify / separator /
    // Verify Files / Uninstall / Add-or-Remove from Library.
    //
    // Separators are never leading, trailing or doubled after the invisible
    // entries around them have been dropped.
    int build_game_menu(const GameMenuFlags &flags, GameMenuEntry *out, int cap);

    // Label for the primary split button in the current state.
    const char *game_primary_label(const GameMenuFlags &flags, bool starting,
                                   bool stopping, bool installing);

    // Command the primary button issues in the current state.
    int game_primary_command(const GameMenuFlags &flags);

} // namespace launcher

#endif // LAUNCHER_APP_GAME_MENU_H
