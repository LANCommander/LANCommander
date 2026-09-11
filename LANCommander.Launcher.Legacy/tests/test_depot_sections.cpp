#include "test_main.h"

#include "app/depot_sections.h"

using namespace launcher;

namespace
{
    // Five games with deliberately awkward data: an undated release, a
    // reversed alphabetical order relative to index, and mixed library and
    // multiplayer flags.
    struct Fixture
    {
        std::vector<std::string> ids, titles, sort_titles, created, released;
        std::vector<bool> library_flags, mp_flags;

        // std::vector<bool> is a bitfield and cannot hand out a bool*, so the
        // view is backed by plain arrays.
        bool in_library[5];
        bool has_mp[5];

        DepotGamesView view;

        Fixture()
        {
            const char *id_v[]      = { "a", "b", "c", "d", "e" };
            const char *title_v[]   = { "Zeta", "Alpha", "Mu", "Beta", "Omega" };
            const char *created_v[] = { "2026-01-05T00:00:00Z",
                                        "2026-01-01T00:00:00Z",
                                        "2026-01-09T00:00:00Z",
                                        "2026-01-03T00:00:00Z",
                                        "2026-01-07T00:00:00Z" };
            // "c" has never been released.
            const char *released_v[] = { "2020-06-01T00:00:00Z",
                                         "2024-02-01T00:00:00Z",
                                         "",
                                         "2022-09-01T00:00:00Z",
                                         "2025-01-01T00:00:00Z" };
            const bool lib_v[] = { true, false, true, false, false };
            const bool mp_v[]  = { true, true, false, false, true };

            for (int i = 0; i < 5; ++i)
            {
                ids.push_back(id_v[i]);
                titles.push_back(title_v[i]);
                sort_titles.push_back(title_v[i]);
                created.push_back(created_v[i]);
                released.push_back(released_v[i]);
                in_library[i] = lib_v[i];
                has_mp[i] = mp_v[i];
            }

            view.ids = &ids[0];
            view.titles = &titles[0];
            view.sort_titles = &sort_titles[0];
            view.created_on = &created[0];
            view.released_on = &released[0];
            view.in_library = in_library;
            view.has_multiplayer = has_mp;
            view.count = 5;
        }
    };

    void test_popular()
    {
        Fixture f;
        std::vector<int> out;

        // CreatedOn descending: c(01-09), e(01-07), a(01-05), d(01-03), b(01-01)
        depot_popular_indices(f.view, 10, &out);
        CHECK_INT(out.size(), 5);
        CHECK_INT(out[0], 2);
        CHECK_INT(out[1], 4);
        CHECK_INT(out[2], 0);
        CHECK_INT(out[3], 3);
        CHECK_INT(out[4], 1);

        depot_popular_indices(f.view, 2, &out);
        CHECK_INT(out.size(), 2);
        CHECK_INT(out[0], 2);

        depot_popular_indices(f.view, 0, &out);
        CHECK_INT(out.size(), 0);

        DepotGamesView empty;
        depot_popular_indices(empty, 10, &out);
        CHECK_INT(out.size(), 0);
    }

    void test_new_releases()
    {
        Fixture f;
        std::vector<int> out;

        depot_new_release_indices(f.view, 10, &out);
        CHECK_INT(out.size(), 5);

        // Newest first: e(2025), b(2024), d(2022), a(2020)...
        CHECK_INT(out[0], 4);
        CHECK_INT(out[1], 1);
        CHECK_INT(out[2], 3);
        CHECK_INT(out[3], 0);

        // ...and the game with NO release date sorts last, not first. An
        // empty string is lexicographically smallest, so a plain descending
        // comparison would put every undated game at the top of the section.
        CHECK_INT(out[4], 2);

        // The cap must not accidentally admit the undated entry.
        depot_new_release_indices(f.view, 3, &out);
        CHECK_INT(out.size(), 3);
        for (size_t i = 0; i < out.size(); ++i)
            CHECK(out[i] != 2);
    }

    void test_all_dates_empty()
    {
        // Every game undated: the result must be stable input order rather
        // than whatever the sort happens to do with an all-equal comparator.
        Fixture f;
        for (int i = 0; i < 5; ++i)
            f.released[i] = "";
        f.view.released_on = &f.released[0];

        std::vector<int> out;
        depot_new_release_indices(f.view, 10, &out);
        CHECK_INT(out.size(), 5);
        for (int i = 0; i < 5; ++i)
            CHECK_INT(out[i], i);
    }

    void test_ties_are_stable()
    {
        Fixture f;
        for (int i = 0; i < 5; ++i)
            f.created[i] = "2026-01-01T00:00:00Z";
        f.view.created_on = &f.created[0];

        std::vector<int> out;
        depot_popular_indices(f.view, 10, &out);
        CHECK_INT(out.size(), 5);
        for (int i = 0; i < 5; ++i)
            CHECK_INT(out[i], i);
    }

    void test_multiplayer_and_backlog()
    {
        Fixture f;
        std::vector<int> out;

        // Multiplayer: a(Zeta), b(Alpha), e(Omega) -> by title: Alpha, Omega, Zeta
        depot_multiplayer_indices(f.view, 20, &out);
        CHECK_INT(out.size(), 3);
        CHECK_INT(out[0], 1);
        CHECK_INT(out[1], 4);
        CHECK_INT(out[2], 0);

        // In library: a(Zeta), c(Mu) -> by title: Mu, Zeta
        depot_backlog_indices(f.view, 20, &out);
        CHECK_INT(out.size(), 2);
        CHECK_INT(out[0], 2);
        CHECK_INT(out[1], 0);

        depot_backlog_indices(f.view, 1, &out);
        CHECK_INT(out.size(), 1);
        CHECK_INT(out[0], 2);

        // Nothing flagged yields an empty section, not the whole catalogue.
        {
            Fixture g;
            for (int i = 0; i < 5; ++i)
                g.in_library[i] = false;
            depot_backlog_indices(g.view, 20, &out);
            CHECK_INT(out.size(), 0);
        }
    }

    void test_search()
    {
        Fixture f;
        std::vector<int> out;

        // Case-insensitive.
        depot_search_indices(f.view, "alpha", &out);
        CHECK_INT(out.size(), 1);
        CHECK_INT(out[0], 1);

        depot_search_indices(f.view, "ALPHA", &out);
        CHECK_INT(out.size(), 1);

        // Substring, not prefix: "eta" matches both Zeta and Beta.
        depot_search_indices(f.view, "eta", &out);
        CHECK_INT(out.size(), 2);
        CHECK_INT(out[0], 0);  // input order preserved
        CHECK_INT(out[1], 3);

        // A single character that appears in several titles.
        depot_search_indices(f.view, "m", &out);
        CHECK_INT(out.size(), 2);  // Mu, Omega

        // No match.
        depot_search_indices(f.view, "zzzz", &out);
        CHECK_INT(out.size(), 0);

        // An empty query matches nothing rather than everything. Returning
        // the whole catalogue would look like the filter had silently failed.
        depot_search_indices(f.view, "", &out);
        CHECK_INT(out.size(), 0);

        // Matching on sort title when the display title does not match.
        {
            Fixture g;
            g.titles[0] = "The Zeta Program";
            g.sort_titles[0] = "Zeta Program, The";
            g.view.titles = &g.titles[0];
            g.view.sort_titles = &g.sort_titles[0];

            depot_search_indices(g.view, "Program, The", &out);
            CHECK_INT(out.size(), 1);
            CHECK_INT(out[0], 0);
        }

        // A query longer than every title must not read out of bounds.
        depot_search_indices(f.view, "a-very-long-query-longer-than-any-title", &out);
        CHECK_INT(out.size(), 0);
    }

    void test_view_adapter()
    {
        Fixture f;
        const GamesView g = depot_as_games_view(f.view);
        CHECK_INT(g.count, 5);
        CHECK(g.ids == f.view.ids);
        CHECK(g.sort_titles == f.view.sort_titles);
    }
} // namespace

void test_depot_sections()
{
    test_popular();
    test_new_releases();
    test_all_dates_empty();
    test_ties_are_stable();
    test_multiplayer_and_backlog();
    test_search();
    test_view_adapter();
}
