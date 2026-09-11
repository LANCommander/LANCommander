// screen_library.cpp — the library and depot pages.
//
// Layout mirrors the Avalonia LibraryRowView: a fixed-width sidebar listing
// every game compactly on the left, and on the right a stack of carousels
// above the cover grid.
//
// The depot is its own screen now (screen_depot.cpp); this page shows the
// user's library only.

#include "ui/screen_library.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/widgets_collection.h"
#include "ui/window_chrome.h"
#include "ui/image_cache.h"
#include "ui/layout.h"
#include "ui/font.h"
#include "app/app.h"
#include "app/game_database.h"
#include "app/library_sections.h"

#include "gfx/gfx.h"

#include <algorithm>
#include <cctype>

namespace launcher
{
    namespace ui
    {

        // --- Persistent state ------------------------------------------------

        static ScrollState s_scroll;          // right-hand page
        static ListState s_sidebar;           // left-hand compact list
        static CarouselState s_recent_carousel;
        static CarouselState s_collections_carousel;

        // Derived views are rebuilt only when the underlying data changes.
        // This is the immediate-mode equivalent of a computed property: it
        // cannot go stale, and it does not rebuild on every frame.
        static unsigned s_seen_revision = 0;
        static bool s_sections_valid = false;

        static std::vector<int> s_recent;
        static std::vector<std::string> s_collection_names;
        static std::vector<int> s_collection_reps;

        // --- Metrics ----------------------------------------------------------

        static const int SIDEBAR_W = 250;
        static const int SIDEBAR_ROW_H = 34;
        static const int ICON_SIZE = 24;

        // Avalonia's UniformGridLayout MinItemWidth is 140 against a 16px
        // base; at this launcher's 13 that is 112. 130 made the grid drop to
        // its 4-column floor on any window narrower than the sidebar plus
        // four wide cells.
        static const int MIN_COVER_W = 112;
        static const int MIN_COL_SPACING = 12;
        static const int MIN_ROW_SPACING = 12;
        static const int GRID_PAD = 20;

        static const int RECENT_ITEM_W = 160;
        static const int RECENT_ITEM_H = 240;
        static const int TILE_W = 200;
        static const int TILE_H = 150;
        static const int CAROUSEL_GAP = 16;
        static const int SECTION_GAP = 20;

        static const int RECENT_MAX = 15;

        // --- Accessors over whichever list is active ---------------------------

        static int active_count(App &app)
        {
            return (int)app.library_data().games.size();
        }

        static std::string get_cover_id(App &app, int index)
        {
            if (index < 0 || index >= (int)app.library_data().games.size())
                return std::string();

            const lancommander::Game &g = app.library_data().games[index];
            if (!g.cover_media_id.empty())
                return g.cover_media_id;

            return app.game_art(g.id).cover;
        }

        static std::string get_icon_id(App &app, int index)
        {
            if (index < 0 || index >= (int)app.library_data().games.size())
                return std::string();

            // From the shared art index rather than the game's own media
            // array. Both are filled from the same /api/Library/Games
            // response, but the index survives the filtering that builds
            // library_data and is what the depot reads too.
            return app.game_art(app.library_data().games[index].id).icon;
        }

        static void get_item(App &app, int index, std::string &id, const char *&title)
        {
            id = app.library_data().games[index].id;
            title = app.library_data().games[index].title.c_str();
        }

        static bool is_installed(App &app, int index)
        {
            if (index < 0 || index >= (int)app.library_data().games.size())
                return false;
            return !app.library_data().games[index].install_directory.empty();
        }

        // --- Section rebuild ---------------------------------------------------

        static void rebuild_sections(App &app)
        {
            s_recent.clear();
            s_collection_names.clear();
            s_collection_reps.clear();
            s_sections_valid = true;

            const std::vector<lancommander::Game> &games = app.library_data().games;
            if (games.empty())
                return;

            std::vector<std::string> ids;
            std::vector<std::string> sort_titles;
            ids.reserve(games.size());
            sort_titles.reserve(games.size());

            for (size_t i = 0; i < games.size(); ++i)
            {
                ids.push_back(games[i].id);
                sort_titles.push_back(games[i].sort_title.empty() ? games[i].title
                                                                  : games[i].sort_title);
            }

            GamesView view;
            view.ids = ids.empty() ? NULL : &ids[0];
            view.sort_titles = sort_titles.empty() ? NULL : &sort_titles[0];
            view.count = (int)ids.size();

            // Recently played is merged from the server's play sessions and
            // this install's local ones -- see App::recent_game_ids.
            //
            // It used to read the local SQLite table alone. That table starts
            // empty on a fresh install, so the carousel was missing for anyone
            // whose history lived on the server, which is everyone who had
            // ever used a different machine or the Avalonia launcher.
            // Every played game, not the first RECENT_MAX of them: the list
            // is then narrowed to games in this library, and plenty of played
            // games are not (a depot game tried and removed, a game shared
            // from another account). Capping before that filter is what would
            // leave a five-item "Recently Played" on a busy account.
            std::vector<std::string> recent_ids;
            app.recent_game_ids(0, &recent_ids);
            library_recent_indices(view, recent_ids, RECENT_MAX, &s_recent);

            // Collections are not on the C++ Game model, so they are read
            // across from the depot entry for the same game. Every library
            // game is in the catalogue, so in practice this is the same set
            // the Avalonia launcher reads from its local database.
            const std::vector<lancommander::DepotGame> &depot = app.depot_data().games;
            std::vector<std::vector<std::string> > per_game;
            per_game.resize(games.size());

            for (size_t i = 0; i < games.size(); ++i)
            {
                for (size_t d = 0; d < depot.size(); ++d)
                {
                    if (depot[d].id != games[i].id)
                        continue;
                    for (size_t c = 0; c < depot[d].collections.size(); ++c)
                        per_game[i].push_back(depot[d].collections[c].name);
                    break;
                }
            }

            name_tiles(view, per_game, &s_collection_names, &s_collection_reps);
        }

        // --- Drawing helpers ---------------------------------------------------

        static void draw_cover(App &app, gfx::Surface *buf, const gfx::Rect &r,
                               const std::string &cover_id, const char *title,
                               bool hovered = false)
        {
            CoverStyle style;
            style.hovered = hovered;

            cover_tile(buf, r, app.image_cache(), cover_id, title, style);
        }

        static void select_game(App &app, int index)
        {
            std::string id;
            const char *title = NULL;
            get_item(app, index, id, title);
            app.set_selected_game(id);
            app.switch_screen(Screen::GameDetail);
        }

        // ----------------------------------------------------------------------

        void screen_library_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int top = chrome_height();
            const int th = text_height();

            app.ensure_library_loaded();

            // The depot is loaded too: it is where collection names for
            // library games come from, since the C++ Game model has none.
            app.ensure_depot_loaded();

            const LoadState state = app.library_data().state;
            const std::string &error = app.library_data().error;
            const unsigned revision = app.library_data().revision;

            if (revision != s_seen_revision)
            {
                s_seen_revision = revision;
                s_scroll.offset = 0;
                s_sidebar.scroll.offset = 0;
                s_sidebar.selected = -1;
                s_recent_carousel = CarouselState();
                s_collections_carousel = CarouselState();
                s_sections_valid = false;
            }

            if (!s_sections_valid)
                rebuild_sections(app);

            const int count = active_count(app);
            const int body_y = top;
            const int body_h = sh - top - footer_height();

            // =================================================================
            // Sidebar
            // =================================================================
            const int side_x = 0;
            const int side_w = SIDEBAR_W;

            gfx::fill_rect(buf, gfx::rect(side_x, body_y, side_w, body_h), theme().surface);
            gfx::vline(buf, side_x + side_w - 1, body_y, body_h, theme().divider);

            // The Library/Depot switch used to sit here as a pair of tabs. It
            // is a single button in the bottom-left of the footer now, where
            // the Avalonia shell puts it, so the sidebar is all list.
            const int list_y = body_y;
            int list_h = body_h;

            {
                const ListResult lr = list_begin(buf, side_x, list_y, side_w, list_h,
                                                 count, SIDEBAR_ROW_H, s_sidebar, input);

                for (int i = lr.first_visible; i <= lr.last_visible; ++i)
                {
                    const gfx::Rect row = list_row_rect(lr, side_x, list_y, side_w,
                                                        i, SIDEBAR_ROW_H, s_sidebar);

                    std::string id;
                    const char *title = NULL;
                    get_item(app, i, id, title);

                    const bool installed = is_installed(app, i);

                    // Icon, requested only for rows that are actually visible.
                    const std::string icon_id = get_icon_id(app, i);
                    gfx::Surface *icon = icon_id.empty()
                                             ? NULL
                                             : app.image_cache().get(icon_id, ICON_SIZE, ICON_SIZE);

                    const int iy = row.y + (SIDEBAR_ROW_H - ICON_SIZE) / 2;
                    if (icon)
                    {
                        gfx::blit_alpha(buf, icon,
                                        row.x + 8 + (ICON_SIZE - gfx::surface_width(icon)) / 2,
                                        iy + (ICON_SIZE - gfx::surface_height(icon)) / 2);
                    }
                    else
                    {
                        gfx::fill_rect(buf, gfx::rect(row.x + 8, iy, ICON_SIZE, ICON_SIZE),
                                       theme().panel);
                    }

                    // Not-installed titles are dimmed, matching the Avalonia
                    // row where opacity carries the same meaning.
                    const gfx::Color title_color = installed ? theme().text
                                                             : theme().text_disabled;

                    const int tx = row.x + 8 + ICON_SIZE + 8;
                    const int avail = side_w - (tx - row.x) - 16;

                    int fitted_w = 0;
                    const int fitted = font_fit(title, avail, &fitted_w, FontSize::Body);
                    if (fitted < (int)std::string(title).size())
                    {
                        std::string clipped(title, (size_t)fitted);
                        if (clipped.size() > 1)
                            clipped.erase(clipped.size() - 1);
                        clipped += "...";
                        draw_text(buf, tx, row.y + (SIDEBAR_ROW_H - th) / 2,
                                  title_color, clipped.c_str());
                    }
                    else
                    {
                        draw_text(buf, tx, row.y + (SIDEBAR_ROW_H - th) / 2,
                                  title_color, title);
                    }

                    if (lr.clicked_index == i)
                        select_game(app, i);
                }

                list_end(buf, side_x, list_y, side_w, list_h, count, SIDEBAR_ROW_H,
                         s_sidebar, input);
            }

            // Refresh lives in the title bar, left of the profile button, as
            // it does in the Avalonia shell — it acts on the whole view, not
            // on the sidebar it used to be pinned under.

            // =================================================================
            // Right-hand page
            // =================================================================
            const int page_x = side_w;
            const int page_w = sw - side_w;

            gfx::push_clip(buf, gfx::rect(page_x, body_y, page_w, body_h));

            if (count == 0)
            {
                if (state == LoadState::Failed && !error.empty())
                    draw_text_center(buf, page_x + page_w / 2, body_y + body_h / 2,
                                     theme().error, error.c_str());
                else
                    draw_text_center(buf, page_x + page_w / 2, body_y + body_h / 2,
                                     theme().text_dim,
                                     "Your library is empty");

                gfx::pop_clip(buf);
                return;
            }

            const int content_x = page_x + GRID_PAD;
            const int content_w = page_w - GRID_PAD * 2 - 14; // room for the scrollbar

            // Carousels are handed the band INCLUDING the page padding, so
            // their arrows sit in that padding and their item strips line up
            // with the grid below. See carousel_gutter().
            const int strip_x = page_x + GRID_PAD - carousel_gutter();
            const int strip_w = content_w + carousel_gutter() * 2;

            int y = body_y + GRID_PAD - s_scroll.offset;
            int content_h = GRID_PAD;

            const bool show_sections = true;

            // --- Recently Played ------------------------------------------------
            if (show_sections && !s_recent.empty())
            {
                const int sec_h = carousel_height(RECENT_ITEM_H);

                // Whole sections that are off-screen are skipped before the
                // carousel runs, so they cost nothing.
                if (y + sec_h > body_y && y < body_y + body_h)
                {
                    const CarouselResult cr =
                        carousel_begin(buf, strip_x, y, strip_w, "Recently Played",
                                       (int)s_recent.size(),
                                       RECENT_ITEM_W, RECENT_ITEM_H, CAROUSEL_GAP,
                                       s_recent_carousel, input, false);

                    for (int i = cr.first_visible; i <= cr.last_visible; ++i)
                    {
                        const int gi = s_recent[i];
                        const gfx::Rect r = carousel_item_rect(cr, i, RECENT_ITEM_W,
                                                               RECENT_ITEM_H, CAROUSEL_GAP,
                                                               s_recent_carousel);
                        std::string id;
                        const char *title = NULL;
                        get_item(app, gi, id, title);
                        draw_cover(app, buf, r, get_cover_id(app, gi), title,
                                   cr.hovered_index == i);
                    }

                    carousel_end(buf);

                    if (cr.clicked_index >= 0)
                    {
                        select_game(app, s_recent[cr.clicked_index]);
                        gfx::pop_clip(buf);
                        return;
                    }
                }

                y += sec_h + SECTION_GAP;
                content_h += sec_h + SECTION_GAP;
            }

            // --- Collections -----------------------------------------------------
            if (show_sections && !s_collection_names.empty())
            {
                const int sec_h = carousel_height(TILE_H);

                if (y + sec_h > body_y && y < body_y + body_h)
                {
                    const CarouselResult cr =
                        carousel_begin(buf, strip_x, y, strip_w, "Collections",
                                       (int)s_collection_names.size(),
                                       TILE_W, TILE_H, CAROUSEL_GAP,
                                       s_collections_carousel, input, false);

                    for (int i = cr.first_visible; i <= cr.last_visible; ++i)
                    {
                        const gfx::Rect r = carousel_item_rect(cr, i, TILE_W, TILE_H,
                                                               CAROUSEL_GAP,
                                                               s_collections_carousel);

                        // Background is the cover of the first game in the
                        // collection, scrimmed so the name stays readable.
                        //
                        // Drawn as `object-fit: cover`: a 2:3 portrait cover
                        // fitted into a 4:3 tile leaves most of the tile blank,
                        // which is what these looked like before.
                        const std::string cid = get_cover_id(app, s_collection_reps[i]);
                        if (!draw_image_cover(buf, app.image_cache(), r, cid))
                            gfx::fill_rect(buf, r, theme().panel);

                        gfx::fill_rect_alpha(buf, r, gfx::rgba(26, 10, 59,
                                                               cr.hovered_index == i ? 200 : 160));

                        // GenreCarouselButton sets its caption at 20 against
                        // a base of 16.
                        draw_text_wrap_center(
                            buf, r.x + r.w / 2,
                            r.y + r.h / 2 - text_height(FontSize::Section),
                            r.w - 16, theme().text_bright,
                            s_collection_names[i].c_str(), FontSize::Section);
                    }

                    carousel_end(buf);
                }

                y += sec_h + SECTION_GAP;
                content_h += sec_h + SECTION_GAP;
            }

            // --- Cover grid ------------------------------------------------------
            {
                // A section header, like the carousel titles above it, not a
                // line of body text. LibraryRowView draws its "All Games" at
                // 18 SemiBold against a base of 16.
                const int head_h = text_height(FontSize::Section);

                draw_text(buf, content_x, y, theme().text_bright, "All Games",
                          FontSize::Section);
                y += head_h + 8;
                content_h += head_h + 8;

                // 4..6 columns, matching the Avalonia responsive clamp
                // (GamesGridView.axaml.cs: minCols 4, maxCols 6). The 7 here
                // let the grid run one column past anything that launcher
                // would draw.
                const GridLayout g = grid_layout(content_w + GRID_PAD * 2, count,
                                                 MIN_COVER_W, MIN_COL_SPACING,
                                                 MIN_ROW_SPACING, 3, 2, GRID_PAD, 4, 6);

                int first = 0, last = -1;
                grid_visible_range(g, count, s_scroll.offset - (y - body_y - GRID_PAD),
                                   body_h, &first, &last);

                for (int i = 0; i < count; ++i)
                {
                    const gfx::Rect cell = grid_cell_rect(g, i, page_x, y, 0);

                    // Off-screen cells cost nothing: no image request, no draw.
                    if (cell.y + cell.h < body_y || cell.y > body_y + body_h)
                        continue;

                    std::string id;
                    const char *title = NULL;
                    get_item(app, i, id, title);

                    // Hit-tested against the VISIBLE part of the cell, not
                    // the whole thing. A cell scrolled halfway out of the page
                    // is still drawn (clipped), and testing its full rect made
                    // the hidden half clickable -- which is how a click on the
                    // title bar over a scrolled grid also opened a game.
                    const bool hovered =
                        gfx::rect_contains(cell, input.mouse.x, input.mouse.y) &&
                        input.mouse.y >= body_y &&
                        input.mouse.y < body_y + body_h;

                    draw_cover(app, buf, cell, get_cover_id(app, i), title, hovered);

                    if (hovered)
                    {

                        if (input.mouse.clicked)
                        {
                            select_game(app, i);
                            gfx::pop_clip(buf);
                            return;
                        }
                    }
                }

                content_h += g.content_h;

                // Cache budget: sidebar rows + carousel items + grid cells on
                // screen, rather than grid cells alone.
                {
                    const int rows_visible = (body_h / (g.item_h + g.row_spacing)) + 2;
                    int capacity = g.cols * rows_visible;
                    capacity += (side_w > 0) ? (list_h / SIDEBAR_ROW_H + 2) : 0;
                    capacity += 16; // carousels
                    if (capacity < 32) capacity = 32;
                    app.image_cache().set_capacity(capacity);
                }
            }

            gfx::pop_clip(buf);

            // --- Page scroll ------------------------------------------------------
            const bool over_page = (input.mouse.x >= page_x && input.mouse.y >= body_y &&
                                    input.mouse.y < body_y + body_h);

            if (over_page && input.mouse.wheel_delta != 0)
            {
                // Unconditional over the page, carousels included: they no
                // longer take the wheel for their own horizontal scroll, which
                // is what made the page refuse to move whenever the pointer
                // happened to be resting on a strip.
                s_scroll.offset -= input.mouse.wheel_delta * 40;
            }

            int max_scroll = content_h - body_h;
            if (max_scroll < 0) max_scroll = 0;
            if (s_scroll.offset < 0) s_scroll.offset = 0;
            if (s_scroll.offset > max_scroll) s_scroll.offset = max_scroll;

            scrollbar(buf, sw - 14, body_y, body_h, content_h, body_h, s_scroll, input);
        }

    } // namespace ui
} // namespace launcher
