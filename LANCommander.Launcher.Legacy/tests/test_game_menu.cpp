#include "test_main.h"

#include "app/game_menu.h"

#include <string>
#include <vector>

using namespace launcher;

namespace
{
    const int CAP = 32;

    std::vector<int> commands(const GameMenuFlags &f)
    {
        GameMenuEntry out[CAP];
        const int n = build_game_menu(f, out, CAP);

        std::vector<int> cmds;
        for (int i = 0; i < n; ++i)
            cmds.push_back(out[i].command);
        return cmds;
    }

    bool has(const std::vector<int> &v, int cmd)
    {
        for (size_t i = 0; i < v.size(); ++i)
            if (v[i] == cmd)
                return true;
        return false;
    }

    int index_of(const std::vector<int> &v, int cmd)
    {
        for (size_t i = 0; i < v.size(); ++i)
            if (v[i] == cmd)
                return (int)i;
        return -1;
    }

    // The property that breaks first when this table is edited: a separator
    // must never be first, never be last, and never appear twice running.
    void check_separators_sane(const std::vector<int> &v)
    {
        if (v.empty())
            return;

        CHECK(v.front() != GM_Separator);
        CHECK(v.back() != GM_Separator);

        for (size_t i = 1; i < v.size(); ++i)
            CHECK(!(v[i] == GM_Separator && v[i - 1] == GM_Separator));
    }

    void test_not_installed()
    {
        GameMenuFlags f;
        f.installed = false;
        f.in_library = false;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        // Nothing that operates on files should be offered for a game that is
        // not on disk.
        CHECK(has(v, GM_Install));
        CHECK(has(v, GM_AddToLibrary));
        CHECK(!has(v, GM_Play));
        CHECK(!has(v, GM_Uninstall));
        CHECK(!has(v, GM_BrowseFiles));
        CHECK(!has(v, GM_VerifyFiles));
        CHECK(!has(v, GM_Modify));
        CHECK(!has(v, GM_RemoveFromLibrary));

        // With every file entry gone, both separators are orphaned and must
        // have been dropped entirely.
        CHECK(!has(v, GM_Separator));
        CHECK_INT(v.size(), 2);
    }

    void test_not_installed_in_library()
    {
        GameMenuFlags f;
        f.installed = false;
        f.in_library = true;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        CHECK(has(v, GM_Install));
        CHECK(has(v, GM_RemoveFromLibrary));
        CHECK(!has(v, GM_AddToLibrary));
        CHECK_INT(v.size(), 2);
    }

    void test_installed_no_update()
    {
        GameMenuFlags f;
        f.installed = true;
        f.in_library = true;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        CHECK(has(v, GM_Play));
        CHECK(has(v, GM_BrowseFiles));
        CHECK(has(v, GM_Modify));
        CHECK(has(v, GM_VerifyFiles));
        CHECK(has(v, GM_Uninstall));
        CHECK(has(v, GM_RemoveFromLibrary));

        CHECK(!has(v, GM_Install));
        CHECK(!has(v, GM_Update));
        CHECK(!has(v, GM_PlayNoUpdate));

        // No manuals for this game, so that entry is absent.
        CHECK(!has(v, GM_ViewManual));

        // Order: Play comes before the file operations, which come before the
        // destructive ones.
        CHECK(index_of(v, GM_Play) < index_of(v, GM_BrowseFiles));
        CHECK(index_of(v, GM_BrowseFiles) < index_of(v, GM_VerifyFiles));
        CHECK(index_of(v, GM_VerifyFiles) < index_of(v, GM_Uninstall));

        // Both separators survive here, because there are entries on both
        // sides of each.
        int seps = 0;
        for (size_t i = 0; i < v.size(); ++i)
            if (v[i] == GM_Separator)
                ++seps;
        CHECK_INT(seps, 2);
    }

    void test_installed_with_update()
    {
        GameMenuFlags f;
        f.installed = true;
        f.in_library = true;
        f.update_available = true;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        CHECK(has(v, GM_Update));
        CHECK(has(v, GM_PlayNoUpdate));
        CHECK(index_of(v, GM_Update) < index_of(v, GM_PlayNoUpdate));
    }

    void test_running()
    {
        GameMenuFlags f;
        f.installed = true;
        f.in_library = true;
        f.update_available = true;
        f.running = true;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        // Updating a game while it is running is not offered, but playing
        // without updating still is.
        CHECK(!has(v, GM_Update));
        CHECK(has(v, GM_PlayNoUpdate));
        CHECK(has(v, GM_Play));
    }

    void test_secondary_actions()
    {
        GameMenuFlags f;
        f.installed = true;
        f.in_library = true;
        f.secondary_count = 3;

        const std::vector<int> v = commands(f);
        check_separators_sane(v);

        CHECK(has(v, GM_SecondaryBase + 0));
        CHECK(has(v, GM_SecondaryBase + 1));
        CHECK(has(v, GM_SecondaryBase + 2));
        CHECK(!has(v, GM_SecondaryBase + 3));

        // They sit between the play verbs and the file operations.
        CHECK(index_of(v, GM_Play) < index_of(v, GM_SecondaryBase));
        CHECK(index_of(v, GM_SecondaryBase + 2) < index_of(v, GM_BrowseFiles));

        // A game that is not installed has no actions to run, so they are
        // suppressed even when the count says otherwise.
        {
            GameMenuFlags g;
            g.installed = false;
            g.secondary_count = 3;
            const std::vector<int> w = commands(g);
            CHECK(!has(w, GM_SecondaryBase));
        }

        // An absurd count is clamped rather than overrunning the table.
        {
            GameMenuFlags g;
            g.installed = true;
            g.secondary_count = 9999;
            GameMenuEntry out[CAP];
            const int n = build_game_menu(g, out, CAP);
            CHECK(n <= CAP);
            CHECK(n > 0);
        }
    }

    void test_enabled_state()
    {
        GameMenuEntry out[CAP];

        // Offline disables anything that needs the server, without hiding it.
        {
            GameMenuFlags f;
            f.installed = true;
            f.in_library = true;
            f.offline = true;

            const int n = build_game_menu(f, out, CAP);
            for (int i = 0; i < n; ++i)
            {
                if (out[i].command == GM_Modify || out[i].command == GM_RemoveFromLibrary)
                    CHECK(!out[i].enabled);
                if (out[i].command == GM_Play || out[i].command == GM_BrowseFiles)
                    CHECK(out[i].enabled);
            }
        }

        // An operation already in progress disables its own entry.
        {
            GameMenuFlags f;
            f.installed = true;
            f.verifying = true;
            f.uninstalling = true;

            const int n = build_game_menu(f, out, CAP);
            for (int i = 0; i < n; ++i)
            {
                if (out[i].command == GM_VerifyFiles || out[i].command == GM_Uninstall)
                    CHECK(!out[i].enabled);
            }
        }
    }

    void test_manuals()
    {
        GameMenuFlags f;
        f.installed = true;
        f.has_manuals = true;

        const std::vector<int> v = commands(f);
        CHECK(has(v, GM_ViewManual));
        CHECK(index_of(v, GM_BrowseFiles) < index_of(v, GM_ViewManual));
    }

    void test_cap_respected()
    {
        GameMenuFlags f;
        f.installed = true;
        f.in_library = true;
        f.update_available = true;
        f.has_manuals = true;
        f.secondary_count = 8;

        GameMenuEntry out[4];
        const int n = build_game_menu(f, out, 4);
        CHECK_INT(n, 4);

        CHECK_INT(build_game_menu(f, out, 0), 0);
        CHECK_INT(build_game_menu(f, NULL, 4), 0);
    }

    void test_primary_button()
    {
        GameMenuFlags f;

        // Not installed.
        CHECK_EQ(std::string(game_primary_label(f, false, false, false)),
                 std::string("Install"));
        CHECK_EQ(std::string(game_primary_label(f, false, false, true)),
                 std::string("Installing..."));
        CHECK_INT(game_primary_command(f), GM_Install);

        // Installed, nothing special.
        f.installed = true;
        CHECK_EQ(std::string(game_primary_label(f, false, false, false)),
                 std::string("Play"));
        CHECK_INT(game_primary_command(f), GM_Play);

        // Starting.
        CHECK_EQ(std::string(game_primary_label(f, true, false, false)),
                 std::string("Starting..."));

        // Update available beats Play.
        f.update_available = true;
        CHECK_EQ(std::string(game_primary_label(f, false, false, false)),
                 std::string("Update"));
        CHECK_INT(game_primary_command(f), GM_Update);

        // Running beats update: the button becomes Stop.
        f.running = true;
        CHECK_EQ(std::string(game_primary_label(f, false, false, false)),
                 std::string("Stop"));
        CHECK_EQ(std::string(game_primary_label(f, false, true, false)),
                 std::string("Stopping"));
    }
} // namespace

void test_game_menu()
{
    test_not_installed();
    test_not_installed_in_library();
    test_installed_no_update();
    test_installed_with_update();
    test_running();
    test_secondary_actions();
    test_enabled_state();
    test_manuals();
    test_cap_respected();
    test_primary_button();
}
