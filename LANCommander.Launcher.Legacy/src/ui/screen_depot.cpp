// screen_depot.cpp — the store front.
//
// Seven stacked sections in the order the Avalonia DepotView uses: Popular,
// Search, Genres, New Releases, Collections, Your Backlog, Play Together.
//
// Hero cards are drawn from a Background and a Logo, as the Avalonia HeroCard
// is. Neither is in the /api/Depot payload, which carries only a cover, so
// they are resolved per game from /api/Games/{id} — the same lookup Avalonia's
// FetchGameWithMediaAsync does, but handed to a worker thread rather than done
// inline, because this launcher's HTTP blocks the frame it runs on. Until the
// answer arrives a card falls back to its cover, and a game with no logo falls
// back to its title.
//
// Genre and collection tiles still reuse a member game's cover rather than
// fetching a hero per tile.

#include "ui/screen_depot.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/widgets_collection.h"
#include "ui/window_chrome.h"
#include "ui/image_cache.h"
#include "ui/layout.h"
#include "app/app.h"
#include "app/depot_sections.h"

#include "gfx/gfx.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            // --- Metrics ---
            const int PAGE_PAD = 20;
            const int SECTION_GAP = 22;
            const int CAROUSEL_GAP = 16;

            const int HERO_W = 460;
            const int HERO_H = 215;
            const int COVER_W = 160;
            const int COVER_H = 240;
            const int TILE_W = 200;
            const int TILE_H = 150;

            const int POPULAR_MAX = 10;

            // Floor for the shared image cache while this page is up.
            const int DEPOT_CACHE_MIN = 64;
            const int LIST_MAX = 20;

            // --- State ---
            ScrollState s_page;

            CarouselState s_popular;
            CarouselState s_genres;
            CarouselState s_new;
            CarouselState s_collections;
            CarouselState s_backlog;
            CarouselState s_multiplayer;

            std::string s_search;
            TextEditState s_search_edit;

            // Derived views, rebuilt only when the depot data changes.
            unsigned s_seen_revision = 0;
            bool s_valid = false;

            std::vector<int> s_popular_idx;
            std::vector<int> s_new_idx;
            std::vector<int> s_backlog_idx;
            std::vector<int> s_multiplayer_idx;

            std::vector<std::string> s_genre_names;
            std::vector<int> s_genre_reps;
            std::vector<std::string> s_collection_names;
            std::vector<int> s_collection_reps;

            // Backing arrays for the parallel-array view. Held here so the
            // pointers inside DepotGamesView stay valid for the frame.
            std::vector<std::string> s_ids, s_titles, s_sorts, s_created, s_released;
            std::vector<char> s_in_library, s_has_mp;

            DepotGamesView build_view(App &app)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;

                s_ids.clear(); s_titles.clear(); s_sorts.clear();
                s_created.clear(); s_released.clear();
                s_in_library.clear(); s_has_mp.clear();

                s_ids.reserve(g.size());
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
                // vector<char> rather than vector<bool>, which is a bitfield
                // and cannot hand out a bool*.
                v.in_library = (const bool *)&s_in_library[0];
                v.has_multiplayer = (const bool *)&s_has_mp[0];
                v.count = (int)g.size();
                return v;
            }

            void rebuild(App &app)
            {
                s_valid = true;

                const DepotGamesView v = build_view(app);

                depot_popular_indices(v, POPULAR_MAX, &s_popular_idx);
                depot_new_release_indices(v, LIST_MAX, &s_new_idx);
                depot_backlog_indices(v, LIST_MAX, &s_backlog_idx);
                depot_multiplayer_indices(v, LIST_MAX, &s_multiplayer_idx);

                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;

                std::vector<std::vector<std::string> > genres;
                std::vector<std::vector<std::string> > collections;
                genres.resize(g.size());
                collections.resize(g.size());

                for (size_t i = 0; i < g.size(); ++i)
                {
                    for (size_t k = 0; k < g[i].genres.size(); ++k)
                        genres[i].push_back(g[i].genres[k].name);
                    for (size_t k = 0; k < g[i].collections.size(); ++k)
                        collections[i].push_back(g[i].collections[k].name);
                }

                const GamesView gv = depot_as_games_view(v);
                name_tiles(gv, genres, &s_genre_names, &s_genre_reps);
                name_tiles(gv, collections, &s_collection_names, &s_collection_reps);
            }

            std::string cover_of(App &app, int index)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                if (index < 0 || index >= (int)g.size())
                    return std::string();
                return g[index].cover.id;
            }

            // Art for a depot game, requesting it if this is the first time
            // it has been asked for. Empty until the worker answers, which is
            // why the hero card has a cover fallback.
            GameArt art_of(App &app, int index)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                if (index < 0 || index >= (int)g.size())
                    return GameArt();

                app.request_game_art(g[index].id);
                return app.game_art(g[index].id);
            }

            const char *title_of(App &app, int index)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                if (index < 0 || index >= (int)g.size())
                    return "";
                return g[index].title.c_str();
            }

            void open_game(App &app, int index)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                if (index < 0 || index >= (int)g.size())
                    return;
                app.set_selected_game(g[index].id);
                app.switch_screen(Screen::GameDetail);
            }

            bool in_library(App &app, int game_index)
            {
                const std::vector<lancommander::DepotGame> &g = app.depot_data().games;
                return game_index >= 0 && game_index < (int)g.size() &&
                       g[game_index].in_library;
            }

            void draw_cover_item(App &app, gfx::Surface *buf, const gfx::Rect &r,
                                 int game_index, bool hovered)
            {
                CoverStyle style;
                style.hovered = hovered;
                style.in_library = in_library(app, game_index);

                cover_tile(buf, r, app.image_cache(), cover_of(app, game_index),
                           title_of(app, game_index), style);
            }

            // A hero card: background art filling the card, a bottom gradient,
            // and the logo over it at bottom-left.
            //
            // Mirrors the Avalonia HeroCard, including its fallbacks: no
            // Background means the cover is used instead (cropped to fill and
            // darkened, standing in for Avalonia's blurred-cover backdrop),
            // and no Logo means the title is drawn instead.
            void draw_hero_card(App &app, gfx::Surface *buf, const gfx::Rect &r,
                                int game_index, bool hovered)
            {
                const GameArt art = art_of(app, game_index);

                bool drew_bg = false;
                if (!art.background.empty())
                    drew_bg = draw_image_cover(buf, app.image_cache(), r, art.background);

                if (!drew_bg)
                {
                    gfx::fill_rect(buf, r, theme().panel);
                    if (draw_image_cover(buf, app.image_cache(), r, cover_of(app, game_index)))
                        gfx::fill_rect_alpha(buf, r, gfx::rgba(0, 0, 0, 85));
                }

                // Gradient up from the bottom edge, so a logo or title over
                // bright art stays legible.
                const int grad_h = r.h / 2;
                gfx::fill_rect_gradient_v(buf,
                                          gfx::rect(r.x, r.y + r.h - grad_h, r.w, grad_h),
                                          gfx::rgba(0, 0, 0, 0), gfx::rgba(0, 0, 0, 238));

                // Sized off the card, and with the CONTAIN fit so a small
                // logo is enlarged to fill its share of it. The old fixed
                // 190x72 box only ever shrank, so most logos drew at their
                // authored size and looked lost on a 460x215 card.
                const int inset = 14;
                gfx::Surface *logo = NULL;
                if (!art.logo.empty())
                    logo = app.image_cache().get(art.logo, r.w * 42 / 100, r.h / 3,
                                                 ImageFit::Contain);

                if (logo)
                {
                    gfx::blit_alpha(buf, logo, r.x + inset,
                                    r.y + r.h - inset - gfx::surface_height(logo));
                }
                else
                {
                    draw_text(buf, r.x + inset,
                              r.y + r.h - inset - text_height(),
                              theme().text_bright, title_of(app, game_index));
                }

                if (hovered)
                    gfx::draw_rect(buf, r, theme().primary);
            }

            // A named tile: a member game's cover, scrimmed, with the name on
            // top. Used for both genres and collections.
            void draw_name_tile(App &app, gfx::Surface *buf, const gfx::Rect &r,
                                const std::string &name, int rep_index, bool hovered)
            {
                // object-fit: cover. A portrait cover fitted into a landscape
                // tile leaves most of the tile empty.
                if (!draw_image_cover(buf, app.image_cache(), r, cover_of(app, rep_index)))
                    gfx::fill_rect(buf, r, theme().panel);

                gfx::fill_rect_alpha(buf, r, gfx::rgba(26, 10, 59, hovered ? 205 : 165));
                draw_text_wrap_center(buf, r.x + r.w / 2,
                                      r.y + r.h / 2 - text_height(FontSize::Section),
                                      r.w - 16, theme().text_bright, name.c_str(),
                                      FontSize::Section);
            }
        } // namespace

        void screen_depot_reset()
        {
            s_valid = false;
            s_page.offset = 0;
        }

        void screen_depot_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int top = chrome_height();
            const int th = text_height();

            app.ensure_depot_loaded();

            // Hero backgrounds and logos come from the art index, which the
            // depot payload does not carry.
            app.ensure_game_art_loaded();

            const DepotData &data = app.depot_data();

            if (data.revision != s_seen_revision)
            {
                s_seen_revision = data.revision;
                s_valid = false;
                s_page.offset = 0;
            }

            if (!s_valid)
                rebuild(app);

            const int body_y = top;
            const int body_h = sh - top - footer_height();

            // The Library/Depot tabs that used to sit above this page are now
            // a single button in the bottom-left of the footer, so the whole
            // body is content.
            const int content_y = body_y;
            const int content_h_avail = body_h;

            gfx::push_clip(buf, gfx::rect(0, content_y, sw, content_h_avail));

            if (data.games.empty())
            {
                if (data.state == LoadState::Failed && !data.error.empty())
                    draw_text_center(buf, sw / 2, content_y + content_h_avail / 2,
                                     theme().error, data.error.c_str());
                else
                    draw_text_center(buf, sw / 2, content_y + content_h_avail / 2,
                                     theme().text_dim, "No games available");
                gfx::pop_clip(buf);
                return;
            }

            // Three sections are on screen at once at most, and the widest of
            // them holds roughly a dozen images — plus a logo per hero card.
            // The library sets this from its own grid, so without a floor here
            // the depot inherits a budget sized for a different page and
            // evicts art it is about to ask for again.
            if (app.image_cache().capacity() < DEPOT_CACHE_MIN)
                app.image_cache().set_capacity(DEPOT_CACHE_MIN);

            const int cx = PAGE_PAD;
            const int cw = sw - PAGE_PAD * 2 - 14; // leave room for the scrollbar

            // Carousels get the band INCLUDING the page padding so their
            // arrows sit in it and their item strips start at cx, in line with
            // the search box. See carousel_gutter().
            const int strip_x = cx - carousel_gutter();
            const int strip_w = cw + carousel_gutter() * 2;

            int y = content_y + PAGE_PAD - s_page.offset;
            int total_h = PAGE_PAD;
            int clicked_game = -1;

            // A section whose band is entirely off-screen is skipped before
            // the carousel runs, so it costs neither layout nor image
            // requests. With seven sections that is the difference between a
            // usable page and a stalled one.
            #define SECTION_VISIBLE(h) ((y) + (h) > content_y && (y) < content_y + content_h_avail)

            // --- 1. Popular -----------------------------------------------
            if (!s_popular_idx.empty())
            {
                const int sec_h = carousel_height(HERO_H);
                if (SECTION_VISIBLE(sec_h))
                {
                    const CarouselResult r =
                        carousel_begin(buf, strip_x, y, strip_w, "Popular",
                                       (int)s_popular_idx.size(),
                                       HERO_W, HERO_H, CAROUSEL_GAP, s_popular, input, true);

                    for (int i = r.first_visible; i <= r.last_visible; ++i)
                    {
                        const gfx::Rect ir = carousel_item_rect(r, i, HERO_W, HERO_H,
                                                                CAROUSEL_GAP, s_popular);
                        draw_hero_card(app, buf, ir, s_popular_idx[i],
                                       r.hovered_index == i);
                    }

                    carousel_end(buf);

                    if (r.clicked_index >= 0)
                        clicked_game = s_popular_idx[r.clicked_index];
                    if (r.see_all_clicked)
                    {
                        app.set_depot_filter(DepotFilterKind::None, "");
                        app.switch_screen(Screen::DepotBrowse);
                        gfx::pop_clip(buf);
                        return;
                    }
                }
                y += sec_h + SECTION_GAP;
                total_h += sec_h + SECTION_GAP;
            }

            // --- 2. Search -------------------------------------------------
            {
                const int sec_h = th + 6 + 24;
                if (SECTION_VISIBLE(sec_h))
                {
                    // Lined up with the carousel item strips above and below
                    // rather than the page edge, and spanning the whole of it —
                    // the Avalonia search row shares the carousels' column
                    // group for exactly this reason. It used to be capped at
                    // 420px, which left it stranded next to full-width
                    // sections.
                    draw_text(buf, cx, y, theme().text, "Search");

                    const TextInputState st =
                        text_input(buf, cx, y + th + 6, cw, 24, s_search, 128,
                                   true, input, s_search_edit);

                    if (st.submitted && !s_search.empty())
                    {
                        app.set_depot_filter(DepotFilterKind::Search, s_search);
                        app.switch_screen(Screen::DepotBrowse);
                        gfx::pop_clip(buf);
                        return;
                    }
                }
                y += sec_h + SECTION_GAP;
                total_h += sec_h + SECTION_GAP;
            }

            // --- 3/5. Named tile sections ----------------------------------
            struct TileSection
            {
                const char *title;
                const std::vector<std::string> *names;
                const std::vector<int> *reps;
                CarouselState *state;
                DepotFilterKind kind;
            };

            const TileSection tiles[] = {
                { "Genres", &s_genre_names, &s_genre_reps, &s_genres, DepotFilterKind::Genre },
                { "Collections", &s_collection_names, &s_collection_reps,
                  &s_collections, DepotFilterKind::Collection },
            };

            // --- Ordered section walk ---------------------------------------
            //
            // Interleaved by hand rather than looped, because the order is
            // Genres, New Releases, Collections, Backlog, Play Together and a
            // single loop cannot express that.

            for (int pass = 0; pass < 5; ++pass)
            {
                const char *title = NULL;
                const std::vector<int> *idx = NULL;
                CarouselState *state = NULL;
                int item_w = COVER_W, item_h = COVER_H;
                const TileSection *tile = NULL;
                bool see_all = false;

                switch (pass)
                {
                case 0: tile = &tiles[0]; item_w = TILE_W; item_h = TILE_H; break;
                case 1: title = "New Releases"; idx = &s_new_idx; state = &s_new; see_all = true; break;
                case 2: tile = &tiles[1]; item_w = TILE_W; item_h = TILE_H; break;
                case 3: title = "Your Backlog"; idx = &s_backlog_idx; state = &s_backlog; break;
                case 4: title = "Play Together"; idx = &s_multiplayer_idx; state = &s_multiplayer; see_all = true; break;
                }

                const int n = tile ? (int)tile->names->size() : (int)idx->size();
                if (n == 0)
                    continue;

                const int sec_h = carousel_height(item_h);

                if (SECTION_VISIBLE(sec_h))
                {
                    const CarouselResult r =
                        carousel_begin(buf, strip_x, y, strip_w,
                                       tile ? tile->title : title, n,
                                       item_w, item_h, CAROUSEL_GAP,
                                       tile ? *tile->state : *state, input, see_all);

                    for (int i = r.first_visible; i <= r.last_visible; ++i)
                    {
                        const gfx::Rect ir =
                            carousel_item_rect(r, i, item_w, item_h, CAROUSEL_GAP,
                                               tile ? *tile->state : *state);

                        if (tile)
                            draw_name_tile(app, buf, ir, (*tile->names)[i],
                                           (*tile->reps)[i], r.hovered_index == i);
                        else
                            draw_cover_item(app, buf, ir, (*idx)[i], r.hovered_index == i);
                    }

                    carousel_end(buf);

                    if (r.clicked_index >= 0)
                    {
                        if (tile)
                        {
                            app.set_depot_filter(tile->kind, (*tile->names)[r.clicked_index]);
                            app.switch_screen(Screen::DepotBrowse);
                            gfx::pop_clip(buf);
                            return;
                        }
                        clicked_game = (*idx)[r.clicked_index];
                    }

                    if (r.see_all_clicked)
                    {
                        app.set_depot_filter(DepotFilterKind::None, "");
                        app.switch_screen(Screen::DepotBrowse);
                        gfx::pop_clip(buf);
                        return;
                    }
                }

                y += sec_h + SECTION_GAP;
                total_h += sec_h + SECTION_GAP;
            }

            #undef SECTION_VISIBLE

            gfx::pop_clip(buf);

            if (clicked_game >= 0)
            {
                open_game(app, clicked_game);
                return;
            }

            // --- Page scroll -------------------------------------------------
            // Unconditional over the page. Carousels no longer take the wheel
            // for their own horizontal scroll, so a pointer resting on a strip
            // no longer stops the page from moving.
            const bool over = (input.mouse.y >= content_y &&
                               input.mouse.y < content_y + content_h_avail);

            if (over && input.mouse.wheel_delta != 0)
                s_page.offset -= input.mouse.wheel_delta * 40;

            int max_scroll = total_h - content_h_avail;
            if (max_scroll < 0) max_scroll = 0;
            if (s_page.offset < 0) s_page.offset = 0;
            if (s_page.offset > max_scroll) s_page.offset = max_scroll;

            scrollbar(buf, sw - 14, content_y, content_h_avail,
                      total_h, content_h_avail, s_page, input);
        }

    } // namespace ui
} // namespace launcher
