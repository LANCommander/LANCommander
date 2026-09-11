#include "app/depot_sections.h"

#include <algorithm>
#include <cctype>

namespace launcher
{

    namespace
    {
        int icmp(const std::string &a, const std::string &b)
        {
            const size_t len = a.size() < b.size() ? a.size() : b.size();
            for (size_t i = 0; i < len; ++i)
            {
                const int ca = std::tolower((unsigned char)a[i]);
                const int cb = std::tolower((unsigned char)b[i]);
                if (ca != cb)
                    return ca < cb ? -1 : 1;
            }
            if (a.size() == b.size())
                return 0;
            return a.size() < b.size() ? -1 : 1;
        }

        bool icontains(const std::string &haystack, const std::string &needle)
        {
            if (needle.empty())
                return false;
            if (needle.size() > haystack.size())
                return false;

            const size_t last = haystack.size() - needle.size();
            for (size_t i = 0; i <= last; ++i)
            {
                size_t j = 0;
                while (j < needle.size())
                {
                    const int a = std::tolower((unsigned char)haystack[i + j]);
                    const int b = std::tolower((unsigned char)needle[j]);
                    if (a != b)
                        break;
                    ++j;
                }
                if (j == needle.size())
                    return true;
            }
            return false;
        }

        // Sorts indices by a date field, newest first, with missing dates
        // pushed to the end. Ties break on index so the order is stable and
        // reproducible rather than dependent on the sort implementation.
        struct DateDesc
        {
            const std::string *dates;

            explicit DateDesc(const std::string *d) : dates(d) {}

            bool operator()(int a, int b) const
            {
                const bool a_empty = dates[a].empty();
                const bool b_empty = dates[b].empty();

                // Undated entries go last regardless of direction. Without
                // this they would sort FIRST, since "" is lexicographically
                // smaller than any real timestamp and this is a descending
                // comparison.
                if (a_empty != b_empty)
                    return b_empty;

                if (!a_empty && dates[a] != dates[b])
                    return dates[a] > dates[b];

                return a < b;
            }
        };

        struct TitleAsc
        {
            const std::string *titles;

            explicit TitleAsc(const std::string *t) : titles(t) {}

            bool operator()(int a, int b) const
            {
                const int c = icmp(titles[a], titles[b]);
                if (c != 0)
                    return c < 0;
                return a < b;
            }
        };

        void take(std::vector<int> &v, int max)
        {
            if (max >= 0 && (int)v.size() > max)
                v.resize((size_t)max);
        }
    } // namespace

    void depot_popular_indices(const DepotGamesView &view, int max,
                               std::vector<int> *out)
    {
        if (!out)
            return;
        out->clear();

        if (view.count <= 0 || !view.created_on || max <= 0)
            return;

        for (int i = 0; i < view.count; ++i)
            out->push_back(i);

        std::sort(out->begin(), out->end(), DateDesc(view.created_on));
        take(*out, max);
    }

    void depot_new_release_indices(const DepotGamesView &view, int max,
                                   std::vector<int> *out)
    {
        if (!out)
            return;
        out->clear();

        if (view.count <= 0 || !view.released_on || max <= 0)
            return;

        for (int i = 0; i < view.count; ++i)
            out->push_back(i);

        std::sort(out->begin(), out->end(), DateDesc(view.released_on));
        take(*out, max);
    }

    void depot_multiplayer_indices(const DepotGamesView &view, int max,
                                   std::vector<int> *out)
    {
        if (!out)
            return;
        out->clear();

        if (view.count <= 0 || !view.has_multiplayer || !view.sort_titles || max <= 0)
            return;

        for (int i = 0; i < view.count; ++i)
            if (view.has_multiplayer[i])
                out->push_back(i);

        std::sort(out->begin(), out->end(), TitleAsc(view.sort_titles));
        take(*out, max);
    }

    void depot_backlog_indices(const DepotGamesView &view, int max,
                               std::vector<int> *out)
    {
        if (!out)
            return;
        out->clear();

        if (view.count <= 0 || !view.in_library || !view.sort_titles || max <= 0)
            return;

        for (int i = 0; i < view.count; ++i)
            if (view.in_library[i])
                out->push_back(i);

        std::sort(out->begin(), out->end(), TitleAsc(view.sort_titles));
        take(*out, max);
    }

    void depot_search_indices(const DepotGamesView &view,
                              const std::string &query,
                              std::vector<int> *out)
    {
        if (!out)
            return;
        out->clear();

        if (view.count <= 0 || query.empty())
            return;

        for (int i = 0; i < view.count; ++i)
        {
            const bool hit =
                (view.titles && icontains(view.titles[i], query)) ||
                (view.sort_titles && icontains(view.sort_titles[i], query));

            if (hit)
                out->push_back(i);
        }
    }

    GamesView depot_as_games_view(const DepotGamesView &view)
    {
        GamesView g;
        g.ids = view.ids;
        g.sort_titles = view.sort_titles;
        g.count = view.count;
        return g;
    }

} // namespace launcher
