#include "app/library_sections.h"

#include <algorithm>
#include <cctype>
#include <map>

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

        std::string lower(const std::string &s)
        {
            std::string out = s;
            for (size_t i = 0; i < out.size(); ++i)
                out[i] = (char)std::tolower((unsigned char)out[i]);
            return out;
        }

        struct TileLess
        {
            bool operator()(const std::string &a, const std::string &b) const
            {
                return icmp(a, b) < 0;
            }
        };
    } // namespace

    void library_recent_indices(const GamesView &view,
                                const std::vector<std::string> &recent_ids,
                                int max,
                                std::vector<int> *out_indices)
    {
        if (!out_indices)
            return;
        out_indices->clear();

        if (!view.ids || view.count <= 0 || max <= 0)
            return;

        for (size_t r = 0; r < recent_ids.size(); ++r)
        {
            if ((int)out_indices->size() >= max)
                break;

            for (int i = 0; i < view.count; ++i)
            {
                if (view.ids[i] == recent_ids[r])
                {
                    out_indices->push_back(i);
                    break;
                }
            }
        }
    }

    void name_tiles(const GamesView &view,
                                  const std::vector<std::vector<std::string> > &per_game_names,
                                  std::vector<std::string> *out_names,
                                  std::vector<int> *out_representative)
    {
        if (!out_names || !out_representative)
            return;
        out_names->clear();
        out_representative->clear();

        if (view.count <= 0)
            return;

        // Keyed on the lowercased name so "RTS" and "rts" are one collection,
        // but the first spelling encountered is what gets displayed.
        std::map<std::string, std::pair<std::string, int> > seen;

        const int n = (int)per_game_names.size() < view.count
                          ? (int)per_game_names.size()
                          : view.count;

        for (int i = 0; i < n; ++i)
        {
            const std::vector<std::string> &names = per_game_names[i];
            for (size_t c = 0; c < names.size(); ++c)
            {
                if (names[c].empty())
                    continue;

                const std::string key = lower(names[c]);
                if (seen.find(key) == seen.end())
                    seen[key] = std::make_pair(names[c], i);
            }
        }

        // Sorted by display name, case-insensitively.
        std::vector<std::string> ordered;
        for (std::map<std::string, std::pair<std::string, int> >::const_iterator it = seen.begin();
             it != seen.end(); ++it)
            ordered.push_back(it->second.first);

        std::sort(ordered.begin(), ordered.end(), TileLess());

        for (size_t i = 0; i < ordered.size(); ++i)
        {
            out_names->push_back(ordered[i]);
            out_representative->push_back(seen[lower(ordered[i])].second);
        }
    }

    void name_members(const GamesView &view,
                                    const std::vector<std::vector<std::string> > &per_game_names,
                                    const std::string &group_name,
                                    std::vector<int> *out_indices)
    {
        if (!out_indices)
            return;
        out_indices->clear();

        if (view.count <= 0 || group_name.empty())
            return;

        const int n = (int)per_game_names.size() < view.count
                          ? (int)per_game_names.size()
                          : view.count;

        for (int i = 0; i < n; ++i)
        {
            const std::vector<std::string> &names = per_game_names[i];
            for (size_t c = 0; c < names.size(); ++c)
            {
                if (icmp(names[c], group_name) == 0)
                {
                    out_indices->push_back(i);
                    break;
                }
            }
        }
    }

} // namespace launcher
