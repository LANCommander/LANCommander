// screen_depot_browse.cpp — the filtered catalogue grid.
//
// Where See-all, a genre tile, a collection tile and a search all land. The
// filter is held on App rather than here so the screen can be entered from
// several places without each of them knowing how this page stores state.
//
// Filtering is entirely client-side over the depot payload already in memory,
// which is also how the Avalonia version works: there is no search API.

#include "ui/screen_depot_browse.h"
#include "ui/theme.h"
#include "ui/icons.h"
#include "ui/widgets.h"
#include "ui/widgets_collection.h"
#include "ui/window_chrome.h"
#include "ui/image_cache.h"
#include "ui/layout.h"
#include "app/app.h"
#include "app/depot_sections.h"

#include "gfx/gfx.h"

#include <cstdio>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            const int PAGE_PAD = 20;
            const int MIN_COVER_W = 130;
            const int COL_GAP = 12;
            const int ROW_GAP = 12;

            ScrollState s_scroll;

            // Rebuilt when the depot data or the filter changes.
            unsigned s_seen_revision = 0;
            DepotFilterKind s_seen_kind = DepotFilterKind::None;
            std::string s_seen_value;
            bool s_valid = false;

            std::vector<int> s_results;

            std::vector<std::string> s_ids, s_titles, s_sorts, s_created, s_released;
            std::vector<char> s_in_library, s_has_mp;

            DepotGamesView build_view(App &app)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;

                s_ids.clear(); s_titles.clear(); s_sorts.clear();
                s_created.clear(); s_released.clear();
                s_in_library.clear(); s_has_mp.clear();

                for (size_t i = 0; i < g.size(); ++i)
                {
                    s_ids.push_back(g[i].id);
                    s_titles.push_back(g[i].title);
                    s_sorts.push_back(g[i].sort_title.empty() ? g[i].title : g[i].sort_title);
                    s_created.push_back(g[i].created_on);
                    s_released.push_back(g[i].released_on);
                    s_in_library.push_back(g[i].in_library ? 1 : 0);
                    s_has_mp.push_back(g[i].multiplayer_modes.empty() ? 0 : 1);
                }

                DepotGamesView v;
                if (g.empty())
                    return v;

                v.ids = &s_ids[0];
                v.titles = &s_titles[0];
                v.sort_titles = &s_sorts[0];
                v.created_on = &s_created[0];
                v.released_on = &s_released[0];
                v.in_library = (const bool *)&s_in_library[0];
                v.has_multiplayer = (const bool *)&s_has_mp[0];
                v.count = (int)g.size();
                return v;
            }

            void rebuild(App &app)
            {
                s_valid = true;
                s_results.clear();

                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                const DepotGamesView v = build_view(app);

                const DepotFilterKind kind = app.depot_filter_kind();
                const std::string &value = app.depot_filter_value();

                if (kind == DepotFilterKind::Search)
                {
                    depot_search_indices(v, value, &s_results);
                    return;
                }

                if (kind == DepotFilterKind::Genre || kind == DepotFilterKind::Collection)
                {
                    std::vector<std::vector<std::string> > per_game;
                    per_game.resize(g.size());

                    for (size_t i = 0; i < g.size(); ++i)
                    {
                        if (kind == DepotFilterKind::Genre)
                        {
                            for (size_t k = 0; k < g[i].genres.size(); ++k)
                                per_game[i].push_back(g[i].genres[k].name);
                        }
                        else
                        {
                            for (size_t k = 0; k < g[i].collections.size(); ++k)
                                per_game[i].push_back(g[i].collections[k].name);
                        }
                    }

                    name_members(depot_as_games_view(v), per_game, value, &s_results);
                    return;
                }

                // No filter: the whole catalogue, already sorted by App.
                for (int i = 0; i < v.count; ++i)
                    s_results.push_back(i);
            }

            const char *kind_label(DepotFilterKind k)
            {
                switch (k)
                {
                case DepotFilterKind::Genre:      return "Genre";
                case DepotFilterKind::Collection: return "Collection";
                case DepotFilterKind::Search:     return "Search";
                default:                          return "All Games";
                }
            }
        } // namespace

        void screen_depot_browse_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int top = chrome_height();
            const int th = text_height();

            app.ensure_depot_loaded();

            const DepotData &data = app.depot_data();

            if (data.revision != s_seen_revision ||
                app.depot_filter_kind() != s_seen_kind ||
                app.depot_filter_value() != s_seen_value)
            {
                s_seen_revision = data.revision;
                s_seen_kind = app.depot_filter_kind();
                s_seen_value = app.depot_filter_value();
                s_valid = false;
                s_scroll.offset = 0;
            }

            if (!s_valid)
                rebuild(app);

            const int body_y = top;
            const int body_h = sh - top - footer_height();

            // --- Toolbar ------------------------------------------------------
            const int bar_h = 32;
            gfx::fill_rect(buf, gfx::rect(0, body_y, sw, bar_h), theme().surface);

            int bx = PAGE_PAD;

            if (icon_button(buf, bx, body_y + 4,
                            button_width("Depot", Icon::ArrowLeft), 26,
                            Icon::ArrowLeft, "Depot", input).clicked)
            {
                app.switch_screen(Screen::Depot);
                return;
            }
            bx += 100;

            // The locked filter, stated rather than implied, so it is obvious
            // why the grid is showing a subset.
            {
                char chip[192];
                if (app.depot_filter_kind() == DepotFilterKind::None)
                    std::sprintf(chip, "All Games");
                else
                    std::sprintf(chip, "%s: %.100s", kind_label(app.depot_filter_kind()),
                                 app.depot_filter_value().c_str());

                const int chip_w = text_width(chip) + 16;
                gfx::fill_rect(buf, gfx::rect(bx, body_y + 4, chip_w, 24), theme().panel);
                draw_text(buf, bx + 8, body_y + 4 + (24 - th) / 2, theme().text, chip);
                bx += chip_w + 8;

                if (app.depot_filter_kind() != DepotFilterKind::None)
                {
                    if (icon_button(buf, bx, body_y + 4, 26, 26,
                                    Icon::Close, NULL, input).clicked)
                    {
                        app.set_depot_filter(DepotFilterKind::None, "");
                        return;
                    }
                }
            }

            {
                char count_buf[64];
                std::sprintf(count_buf, "%d games", (int)s_results.size());
                draw_text_right(buf, sw - PAGE_PAD, body_y + 4 + (24 - th) / 2,
                                theme().text_dim, count_buf);
            }

            // --- Grid ----------------------------------------------------------
            const int grid_y = body_y + bar_h;
            const int grid_h = body_h - bar_h;
            const int count = (int)s_results.size();

            gfx::push_clip(buf, gfx::rect(0, grid_y, sw, grid_h));

            if (count == 0)
            {
                draw_text_center(buf, sw / 2, grid_y + grid_h / 2, theme().text_dim,
                                 "No games match this filter");
                gfx::pop_clip(buf);
                return;
            }

            const GridLayout g = grid_layout(sw - 14, count, MIN_COVER_W, COL_GAP,
                                             ROW_GAP, 3, 2, PAGE_PAD, 4, 7);

            int first = 0, last = -1;
            grid_visible_range(g, count, s_scroll.offset, grid_h, &first, &last);

            const std::vector<lancommander::DepotGame> &games = app.depot_data().games;

            for (int i = first; i <= last; ++i)
            {
                const gfx::Rect cell = grid_cell_rect(g, i, 0, grid_y, s_scroll.offset);
                const int gi = s_results[i];
                if (gi < 0 || gi >= (int)games.size())
                    continue;

                // Clipped to the visible band: a cell scrolled halfway out is
                // still drawn, and testing its whole rect made the hidden part
                // clickable through whatever is drawn over it.
                const bool cell_hovered =
                    gfx::rect_contains(cell, input.mouse.x, input.mouse.y) &&
                    input.mouse.y >= grid_y && input.mouse.y < grid_y + grid_h;

                CoverStyle cell_style;
                cell_style.hovered = cell_hovered;
                cell_style.in_library = games[gi].in_library;

                cover_tile(buf, cell, app.image_cache(), games[gi].cover.id,
                           games[gi].title.c_str(), cell_style);

                if (cell_hovered)
                {
                    if (input.mouse.clicked)
                    {
                        app.set_selected_game(games[gi].id);
                        app.switch_screen(Screen::GameDetail);
                        gfx::pop_clip(buf);
                        return;
                    }
                }
            }

            gfx::pop_clip(buf);

            if (input.mouse.wheel_delta != 0 && input.mouse.y >= grid_y)
                s_scroll.offset -= input.mouse.wheel_delta * 40;

            int max_scroll = g.content_h - grid_h;
            if (max_scroll < 0) max_scroll = 0;
            if (s_scroll.offset < 0) s_scroll.offset = 0;
            if (s_scroll.offset > max_scroll) s_scroll.offset = max_scroll;

            scrollbar(buf, sw - 14, grid_y, grid_h, g.content_h, grid_h, s_scroll, input);
        }

    } // namespace ui
} // namespace launcher
