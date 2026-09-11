#ifndef LAUNCHER_APP_DEPOT_SECTIONS_H
#define LAUNCHER_APP_DEPOT_SECTIONS_H

#include <string>
#include <vector>

#include "app/library_sections.h" // GamesView, name_tiles, name_members

// Which games belong in which depot carousel.
//
// SDK-free parallel arrays, same rationale as library_sections.h: these are
// ordering rules with sharp edges (empty dates, ties, caps) and they belong
// somewhere they can be tested without a server.
//
// The selection deliberately mirrors what the Avalonia DepotViewModel derives
// on the client, NOT the `popular` and `backlog` id lists the server puts in
// DepotResults. Those exist and are cheaper, but the Avalonia client ignores
// them, and parity means matching what a user actually sees.

namespace launcher
{

    struct DepotGamesView
    {
        const std::string *ids;
        const std::string *titles;
        const std::string *sort_titles;

        // ISO-8601. Compared as text, which is only correct because that
        // format sorts lexicographically in chronological order.
        const std::string *created_on;
        const std::string *released_on;

        const bool *in_library;
        const bool *has_multiplayer;

        int count;

        DepotGamesView()
            : ids(NULL), titles(NULL), sort_titles(NULL), created_on(NULL),
              released_on(NULL), in_library(NULL), has_multiplayer(NULL),
              count(0) {}
    };

    // Newest additions to the catalogue: CreatedOn descending, capped.
    void depot_popular_indices(const DepotGamesView &view, int max,
                               std::vector<int> *out);

    // ReleasedOn descending, capped.
    //
    // A game with no release date sorts LAST, not first. An empty string is
    // lexicographically smallest, so the obvious comparison would put every
    // undated game at the top of "New Releases".
    void depot_new_release_indices(const DepotGamesView &view, int max,
                                   std::vector<int> *out);

    // Games with any multiplayer mode, by sort title, capped.
    void depot_multiplayer_indices(const DepotGamesView &view, int max,
                                   std::vector<int> *out);

    // Games already in the user's library, by sort title, capped.
    void depot_backlog_indices(const DepotGamesView &view, int max,
                               std::vector<int> *out);

    // Case-insensitive substring match over title and sort title, in the
    // input order. An empty query matches NOTHING rather than everything: the
    // depot search box is a filter the user opts into, and returning the whole
    // catalogue for an empty box would look like the filter had failed.
    void depot_search_indices(const DepotGamesView &view,
                              const std::string &query,
                              std::vector<int> *out);

    // Adapts a DepotGamesView to the GamesView that name_tiles/name_members
    // take, so genre and collection tiles share one implementation with the
    // library's collections.
    GamesView depot_as_games_view(const DepotGamesView &view);

} // namespace launcher

#endif // LAUNCHER_APP_DEPOT_SECTIONS_H
