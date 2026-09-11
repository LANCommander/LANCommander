#ifndef LAUNCHER_APP_LIBRARY_SECTIONS_H
#define LAUNCHER_APP_LIBRARY_SECTIONS_H

#include <string>
#include <vector>

// Which games belong in which section of the library page.
//
// Deliberately free of lancommander:: types, taking parallel arrays instead,
// for the same reason chrome_geometry.cpp takes ints: it lets the selection
// rules compile into launcher_tests without the SDK, and those rules are
// where the off-by-ones live.
//
// Everything returns INDICES into the caller's arrays, never copies. The
// library can be several hundred games and these are rebuilt whenever the
// underlying data changes.

namespace launcher
{

    struct GamesView
    {
        const std::string *ids;
        const std::string *sort_titles;
        int count;

        GamesView() : ids(NULL), sort_titles(NULL), count(0) {}
    };

    // Maps game ids from GameDatabase::recent_games() onto library indices.
    //
    // Order is preserved (the database already sorted by most recent play),
    // ids not present in the library are dropped, and the result is capped at
    // `max`. A game that was played and then removed from the library must
    // not appear, which is why this is a filter rather than a lookup.
    void library_recent_indices(const GamesView &view,
                                const std::vector<std::string> &recent_ids,
                                int max,
                                std::vector<int> *out_indices);

    // Distinct names across a game list, case-insensitively
    // de-duplicated and sorted, each paired with the index of the FIRST game
    // in that collection — whose cover becomes the tile background.
    //
    // `per_game_names` is index-aligned with `view`; a game in three
    // groups contributes to all three. Used for library collections and for
    // the depot genre and collection tiles alike.
    void name_tiles(const GamesView &view,
                                  const std::vector<std::vector<std::string> > &per_game_names,
                                  std::vector<std::string> *out_names,
                                  std::vector<int> *out_representative);

    // Indices of games belonging to one collection, in library order.
    // Backs the filtered view a collection tile navigates to.
    void name_members(const GamesView &view,
                                    const std::vector<std::vector<std::string> > &per_game_names,
                                    const std::string &group_name,
                                    std::vector<int> *out_indices);

} // namespace launcher

#endif // LAUNCHER_APP_LIBRARY_SECTIONS_H
