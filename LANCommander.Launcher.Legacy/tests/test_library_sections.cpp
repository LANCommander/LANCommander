#include "test_main.h"

#include "app/library_sections.h"

using namespace launcher;

namespace
{
    // A small library, deliberately NOT in id order, so any test that passes
    // by accident because index == position would fail here.
    struct Fixture
    {
        std::vector<std::string> ids;
        std::vector<std::string> sort_titles;
        GamesView view;

        Fixture()
        {
            ids.push_back("g-zebra");   // 0
            ids.push_back("g-apple");   // 1
            ids.push_back("g-mango");   // 2
            ids.push_back("g-cherry");  // 3

            sort_titles.push_back("Zebra");
            sort_titles.push_back("Apple");
            sort_titles.push_back("Mango");
            sort_titles.push_back("Cherry");

            view.ids = &ids[0];
            view.sort_titles = &sort_titles[0];
            view.count = 4;
        }
    };

    void test_recent()
    {
        Fixture f;
        std::vector<int> out;

        // The database hands back ids already ordered by most recent play;
        // that order must survive the mapping to indices.
        {
            std::vector<std::string> recent;
            recent.push_back("g-mango");
            recent.push_back("g-zebra");
            recent.push_back("g-cherry");

            library_recent_indices(f.view, recent, 15, &out);
            CHECK_INT(out.size(), 3);
            CHECK_INT(out[0], 2);
            CHECK_INT(out[1], 0);
            CHECK_INT(out[2], 3);
        }

        // A game played and then removed from the library must not appear.
        // This is why the mapping is a filter and not a lookup.
        {
            std::vector<std::string> recent;
            recent.push_back("g-removed");
            recent.push_back("g-apple");

            library_recent_indices(f.view, recent, 15, &out);
            CHECK_INT(out.size(), 1);
            CHECK_INT(out[0], 1);
        }

        // The cap applies to the RESULT, not the input, so dropped ids do not
        // eat a slot.
        {
            std::vector<std::string> recent;
            recent.push_back("g-gone-1");
            recent.push_back("g-zebra");
            recent.push_back("g-gone-2");
            recent.push_back("g-apple");
            recent.push_back("g-mango");

            library_recent_indices(f.view, recent, 2, &out);
            CHECK_INT(out.size(), 2);
            CHECK_INT(out[0], 0);
            CHECK_INT(out[1], 1);
        }

        // Degenerate inputs.
        {
            std::vector<std::string> empty;
            library_recent_indices(f.view, empty, 15, &out);
            CHECK_INT(out.size(), 0);

            std::vector<std::string> recent;
            recent.push_back("g-zebra");
            library_recent_indices(f.view, recent, 0, &out);
            CHECK_INT(out.size(), 0);

            GamesView empty_view;
            library_recent_indices(empty_view, recent, 15, &out);
            CHECK_INT(out.size(), 0);
        }
    }

    void test_collection_tiles()
    {
        Fixture f;
        std::vector<std::string> names;
        std::vector<int> reps;

        // "RTS" and "rts" are one collection; the first spelling seen wins.
        // A game in several collections contributes to all of them.
        std::vector<std::vector<std::string> > per_game;
        per_game.resize(4);
        per_game[0].push_back("RTS");           // zebra
        per_game[1].push_back("rts");           // apple, same collection
        per_game[1].push_back("Shooters");
        per_game[2].push_back("Puzzle");
        per_game[2].push_back("shooters");
        per_game[3];                             // cherry: none

        name_tiles(f.view, per_game, &names, &reps);

        // Sorted case-insensitively by display name.
        CHECK_INT(names.size(), 3);
        CHECK_EQ(names[0], std::string("Puzzle"));
        CHECK_EQ(names[1], std::string("RTS"));
        CHECK_EQ(names[2], std::string("Shooters"));
        CHECK_INT(reps.size(), 3);

        // The representative is the FIRST game in that collection, which is
        // whose cover becomes the tile background.
        CHECK_INT(reps[0], 2); // Puzzle -> mango
        CHECK_INT(reps[1], 0); // RTS    -> zebra, not apple
        CHECK_INT(reps[2], 1); // Shooters -> apple, not mango

        // Empty names are ignored rather than producing a blank tile.
        {
            std::vector<std::vector<std::string> > pg;
            pg.resize(4);
            pg[0].push_back("");
            pg[1].push_back("Real");

            name_tiles(f.view, pg, &names, &reps);
            CHECK_INT(names.size(), 1);
            CHECK_EQ(names[0], std::string("Real"));
        }

        // No collections at all.
        {
            std::vector<std::vector<std::string> > pg;
            pg.resize(4);
            name_tiles(f.view, pg, &names, &reps);
            CHECK_INT(names.size(), 0);
            CHECK_INT(reps.size(), 0);
        }

        // A short per-game array must not read past its end. This happens for
        // real: the collections come from the depot, which can return fewer
        // entries than the library holds.
        {
            std::vector<std::vector<std::string> > pg;
            pg.resize(2);
            pg[0].push_back("OnlyOne");
            name_tiles(f.view, pg, &names, &reps);
            CHECK_INT(names.size(), 1);
            CHECK_INT(reps[0], 0);
        }
    }

    void test_collection_members()
    {
        Fixture f;
        std::vector<int> out;

        std::vector<std::vector<std::string> > per_game;
        per_game.resize(4);
        per_game[0].push_back("RTS");
        per_game[1].push_back("rts");
        per_game[2].push_back("Puzzle");
        per_game[3].push_back("RTS");

        // Membership is case-insensitive and returned in library order.
        name_members(f.view, per_game, "rts", &out);
        CHECK_INT(out.size(), 3);
        CHECK_INT(out[0], 0);
        CHECK_INT(out[1], 1);
        CHECK_INT(out[2], 3);

        name_members(f.view, per_game, "PUZZLE", &out);
        CHECK_INT(out.size(), 1);
        CHECK_INT(out[0], 2);

        // Unknown collection and empty name both yield nothing rather than
        // everything.
        name_members(f.view, per_game, "Nope", &out);
        CHECK_INT(out.size(), 0);
        name_members(f.view, per_game, "", &out);
        CHECK_INT(out.size(), 0);

        // A game listed twice in one collection appears once.
        {
            std::vector<std::vector<std::string> > pg;
            pg.resize(4);
            pg[0].push_back("Dup");
            pg[0].push_back("Dup");
            name_members(f.view, pg, "Dup", &out);
            CHECK_INT(out.size(), 1);
        }
    }
} // namespace

void test_library_sections()
{
    test_recent();
    test_collection_tiles();
    test_collection_members();
}
