#include "ui/screen_library.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "ui/image_cache.h"
#include "app/app.h"
#include "app/game_database.h"

#include <allegro.h>
#include <algorithm>
#include <cctype>

namespace launcher
{
    namespace ui
    {

        static bool s_depot_loaded = false;
        static bool s_library_loaded = false;
        static std::string s_error;
        static int s_scroll_y = 0;

        // Grid constants — minimums; actual sizes expand to fill the row.
        static const int MIN_COVER_W = 130;
        static const int MIN_COL_SPACING = 12;
        static const int MIN_ROW_SPACING = 12;
        static const int GRID_PAD = 20;

        static bool iless(const std::string &a, const std::string &b)
        {
            size_t len = a.size() < b.size() ? a.size() : b.size();
            for (size_t i = 0; i < len; ++i)
            {
                int ca = std::tolower((unsigned char)a[i]);
                int cb = std::tolower((unsigned char)b[i]);
                if (ca != cb)
                    return ca < cb;
            }
            return a.size() < b.size();
        }

        static const std::string &sort_key(const lancommander::DepotGame &g)
        {
            return g.sort_title.empty() ? g.title : g.sort_title;
        }

        static const std::string &sort_key(const lancommander::Game &g)
        {
            return g.sort_title.empty() ? g.title : g.sort_title;
        }

        static bool is_top_level(lancommander::GameType t)
        {
            return t == lancommander::GameType::MainGame
                || t == lancommander::GameType::StandaloneExpansion
                || t == lancommander::GameType::StandaloneMod;
        }

        static void load_depot(App &app)
        {
            auto result = app.depot().get();
            if (result)
            {
                std::vector<lancommander::DepotGame> filtered;
                for (size_t i = 0; i < result.value.games.size(); ++i)
                {
                    if (is_top_level(result.value.games[i].type))
                        filtered.push_back(result.value.games[i]);
                }
                app.depot_cache() = filtered;
                std::sort(app.depot_cache().begin(), app.depot_cache().end(),
                          [](const lancommander::DepotGame &a, const lancommander::DepotGame &b)
                          { return iless(sort_key(a), sort_key(b)); });
                s_error.clear();
            }
            else
                s_error = result.error;
            s_depot_loaded = true;
        }

        static void load_library(App &app)
        {
            // Also load depot data so we can look up covers for library games.
            if (!s_depot_loaded)
                load_depot(app);

            auto result = app.games().get_all();
            if (result)
            {
                std::vector<lancommander::Game> lib;
                for (size_t i = 0; i < result.value.size(); ++i)
                {
                    if (result.value[i].in_library && is_top_level(result.value[i].type))
                    {
                        lancommander::Game g = result.value[i];

                        // If get_all didn't populate cover_media_id, look it up
                        // from the depot cache.
                        if (g.cover_media_id.empty())
                        {
                            for (size_t d = 0; d < app.depot_cache().size(); ++d)
                            {
                                if (app.depot_cache()[d].id == g.id)
                                {
                                    g.cover_media_id = app.depot_cache()[d].cover.id;
                                    break;
                                }
                            }
                        }

                        // Apply local install state from database.
                        InstalledGame local;
                        if (app.game_db().find(g.id, &local))
                            g.install_directory = local.install_directory;

                        lib.push_back(g);
                    }
                }
                app.game_cache() = lib;
                std::sort(app.game_cache().begin(), app.game_cache().end(),
                          [](const lancommander::Game &a, const lancommander::Game &b)
                          { return iless(sort_key(a), sort_key(b)); });
                s_error.clear();
            }
            else
                s_error = result.error;
            s_library_loaded = true;
        }

        // Get the cover media ID for a game at the given index in the active list.
        static std::string get_cover_id(App &app, int index)
        {
            if (app.library_tab() == LibraryTab::Depot)
            {
                if (index < 0 || index >= (int)app.depot_cache().size())
                    return std::string();
                return app.depot_cache()[index].cover.id;
            }
            else
            {
                if (index < 0 || index >= (int)app.game_cache().size())
                    return std::string();
                const lancommander::Game &g = app.game_cache()[index];
                if (!g.cover_media_id.empty())
                    return g.cover_media_id;
                for (size_t i = 0; i < g.media.size(); ++i)
                    if (g.media[i].type == "Cover")
                        return g.media[i].id;
                return std::string();
            }
        }

        static void get_item(App &app, int index, std::string &id, const char *&title)
        {
            if (app.library_tab() == LibraryTab::Depot)
            {
                id = app.depot_cache()[index].id;
                title = app.depot_cache()[index].title.c_str();
            }
            else
            {
                id = app.game_cache()[index].id;
                title = app.game_cache()[index].title.c_str();
            }
        }

        void screen_library_draw(App &app, const InputState &input)
        {
            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();
            int top = chrome_height();

            // Load data on first entry.
            if (app.library_tab() == LibraryTab::Depot && !s_depot_loaded)
            {
                load_depot(app);
                s_scroll_y = 0;
            }
            if (app.library_tab() == LibraryTab::Library && !s_library_loaded)
            {
                load_library(app);
                s_scroll_y = 0;
            }

            // Grid starts below chrome, ends above footer.
            int grid_y = top;
            int grid_area_h = sh - top - footer_height();

            int count = 0;
            if (app.library_tab() == LibraryTab::Depot)
                count = (int)app.depot_cache().size();
            else
                count = (int)app.game_cache().size();

            // ---------------------------------------------------------------
            // UniformGridLayout: compute how many columns fit at the minimum
            // item width, then expand items to fill the row evenly.
            // ---------------------------------------------------------------
            int usable_w = sw - GRID_PAD * 2;

            // How many columns fit? Each column needs at least MIN_COVER_W
            // and there's MIN_COL_SPACING between columns.
            int cols = (usable_w + MIN_COL_SPACING) / (MIN_COVER_W + MIN_COL_SPACING);
            if (cols < 1) cols = 1;
            if (cols > count && count > 0) cols = count;

            // Distribute the remaining space: expand item width and spacing.
            // Total space used by gaps: (cols - 1) * gap.
            // Remaining for items: usable_w - gaps.
            int total_gap = (cols > 1) ? (cols - 1) * MIN_COL_SPACING : 0;
            int item_w = (usable_w - total_gap) / cols;

            // Recompute spacing to fill any leftover pixels evenly.
            int col_spacing = MIN_COL_SPACING;
            if (cols > 1)
            {
                int leftover = usable_w - (cols * item_w);
                col_spacing = leftover / (cols - 1);
            }

            // Item height: maintain 2:3 aspect ratio.
            int item_h = item_w * 3 / 2;
            int row_spacing = MIN_ROW_SPACING;

            int grid_x0 = GRID_PAD;
            int rows = (count + cols - 1) / cols;
            int content_h = GRID_PAD + rows * (item_h + row_spacing);

            // Set the image cache capacity to the number of covers that can
            // fit on screen plus two extra rows as a scroll buffer.
            {
                int visible_rows = (grid_area_h + item_h + row_spacing - 1)
                                   / (item_h + row_spacing);
                int capacity = cols * (visible_rows + 2);
                if (capacity < 16) capacity = 16;
                app.image_cache().set_capacity(capacity);
            }

            // Scroll
            if (input.mouse.wheel_delta != 0 && input.mouse.y >= grid_y)
            {
                s_scroll_y -= input.mouse.wheel_delta * 40;
                if (s_scroll_y < 0) s_scroll_y = 0;
                int max_scroll = content_h - grid_area_h;
                if (max_scroll < 0) max_scroll = 0;
                if (s_scroll_y > max_scroll) s_scroll_y = max_scroll;
            }

            // Clip to grid area.
            set_clip_rect(buf, 0, grid_y, sw - 1, sh - footer_height() - 1);

            if (count == 0)
            {
                if (!s_error.empty())
                    draw_text_center(buf, sw / 2, sh / 2, theme().error, s_error.c_str());
                else
                {
                    const char *msg = (app.library_tab() == LibraryTab::Depot)
                                          ? "No games available"
                                          : "Your library is empty";
                    draw_text_center(buf, sw / 2, sh / 2, theme().text_dim, msg);
                }
            }

            // Draw grid items.
            for (int i = 0; i < count; ++i)
            {
                int col_idx = i % cols;
                int row_idx = i / cols;

                int cx = grid_x0 + col_idx * (item_w + col_spacing);
                int cy = grid_y + GRID_PAD + row_idx * (item_h + row_spacing) - s_scroll_y;

                // Skip items that are fully off-screen.
                if (cy + item_h < grid_y || cy > sh)
                    continue;

                std::string item_id;
                const char *item_title;
                get_item(app, i, item_id, item_title);

                // --- Cover image ---
                std::string cid = get_cover_id(app, i);
                BITMAP *cover = NULL;
                if (!cid.empty())
                    cover = app.image_cache().get(cid, item_w, item_h);

                if (cover)
                {
                    // Center the cover in the cell if decoded size differs.
                    int ix = cx + (item_w - cover->w) / 2;
                    int iy = cy + (item_h - cover->h) / 2;
                    blit(cover, buf, 0, 0, ix, iy, cover->w, cover->h);
                }
                else
                {
                    // Placeholder: dark panel with word-wrapped title.
                    rectfill(buf, cx, cy, cx + item_w - 1, cy + item_h - 1, theme().panel);
                    set_clip_rect(buf, cx, cy, cx + item_w - 1, cy + item_h - 1);
                    int pad = 8;
                    int wrap_w = item_w - pad * 2;
                    int text_h = draw_text_wrap_center(NULL, 0, 0, wrap_w, 0, item_title);
                    int ty = cy + (item_h - text_h) / 2;
                    draw_text_wrap_center(buf, cx + item_w / 2, ty, wrap_w,
                                          theme().text_dim, item_title);
                    set_clip_rect(buf, 0, grid_y, sw - 1, sh - footer_height() - 1);
                }

                // --- Hover highlight ---
                bool hovered = (input.mouse.x >= cx && input.mouse.x < cx + item_w &&
                                input.mouse.y >= cy && input.mouse.y < cy + item_h &&
                                input.mouse.y >= grid_y &&
                                input.mouse.y < sh - footer_height());

                if (hovered)
                {
                    // Light overlay on hover.
                    drawing_mode(DRAW_MODE_TRANS, NULL, 0, 0);
                    set_trans_blender(255, 255, 255, 40);
                    rectfill(buf, cx, cy, cx + item_w - 1, cy + item_h - 1,
                             makecol(255, 255, 255));
                    drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);

                    // Border
                    rect(buf, cx - 1, cy - 1, cx + item_w, cy + item_h,
                         theme().primary);
                }

                // --- Click to open game detail ---
                if (hovered && input.mouse.clicked)
                {
                    app.set_selected_game(item_id);
                    app.switch_screen(Screen::GameDetail);
                }
            }

            // Restore clip rect.
            set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

            // Scrollbar
            scrollbar(buf, sw - 14, grid_y, grid_area_h,
                      content_h, grid_area_h, s_scroll_y, input);

        }

    } // namespace ui
} // namespace launcher
