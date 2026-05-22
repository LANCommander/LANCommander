#include "ui/screen_downloads.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "app/app.h"

#include <allegro.h>
#include <cstdio>

namespace launcher
{
    namespace ui
    {

        static int s_scroll_y = 0;

        static void format_bytes(unsigned long bytes, char *buf, int buf_sz)
        {
            if (bytes >= 1024UL * 1024UL * 1024UL)
                sprintf(buf, "%.1f GB", bytes / (1024.0 * 1024.0 * 1024.0));
            else if (bytes >= 1024UL * 1024UL)
                sprintf(buf, "%.1f MB", bytes / (1024.0 * 1024.0));
            else if (bytes >= 1024UL)
                sprintf(buf, "%.0f KB", bytes / 1024.0);
            else
                sprintf(buf, "%lu B", bytes);
        }

        void screen_downloads_draw(App &app, const InputState &input)
        {
            BITMAP *buf = app.backbuffer();
            int sw = app.screen_width();
            int sh = app.screen_height();

            int top = chrome_height();
            int bottom = sh - footer_height();
            int th = text_height();
            int pad = 16;

            // --- Header bar ---
            int header_h = 40;
            int header_y = top;
            panel(buf, 0, header_y, sw, header_h, theme().surface);
            hline(buf, 0, header_y + header_h - 1, sw - 1, theme().divider);

            // Back button
            int back_w = 60;
            int back_h = 22;
            int back_x = pad;
            int back_y = header_y + (header_h - back_h) / 2;
            ButtonState back_btn = button(buf, back_x, back_y, back_w, back_h, "< Back", input);

            // Title
            draw_text(buf, back_x + back_w + 12, header_y + (header_h - th) / 2,
                      theme().text_bright, "Downloads");

            // Pending count
            int pending = app.downloads().pending_count();
            if (pending > 0)
            {
                char count_str[32];
                sprintf(count_str, "(%d pending)", pending);
                int title_w = text_width("Downloads");
                draw_text(buf, back_x + back_w + 12 + title_w + 8,
                          header_y + (header_h - th) / 2,
                          theme().text_dim, count_str);
            }

            // Clear finished button (right side)
            bool has_finished = false;
            const std::vector<DownloadItem> &items = app.downloads().items();
            for (size_t i = 0; i < items.size(); ++i)
            {
                if (items[i].status == DownloadStatus::Complete ||
                    items[i].status == DownloadStatus::Failed)
                {
                    has_finished = true;
                    break;
                }
            }

            if (has_finished)
            {
                const char *clear_label = "Clear Finished";
                int clear_w = text_width(clear_label) + 20;
                int clear_x = sw - pad - clear_w;
                int clear_y = header_y + (header_h - back_h) / 2;
                ButtonState clear_btn = button(buf, clear_x, clear_y, clear_w, back_h,
                                               clear_label, input);
                if (clear_btn.clicked)
                    app.downloads().clear_finished();
            }

            // --- Content area ---
            int content_y = header_y + header_h;
            int content_h = bottom - content_y;
            int item_h = 56;
            int bar_h = 4;

            // Total content height for scrolling
            int total_h = (int)items.size() * item_h;
            if (items.empty())
                total_h = content_h; // no scroll needed

            // Scroll
            if (input.mouse.y >= content_y && input.mouse.y < bottom &&
                input.mouse.wheel_delta != 0)
            {
                s_scroll_y -= input.mouse.wheel_delta * 28;
                if (s_scroll_y < 0) s_scroll_y = 0;
                int max_scroll = total_h - content_h;
                if (max_scroll < 0) max_scroll = 0;
                if (s_scroll_y > max_scroll) s_scroll_y = max_scroll;
            }

            set_clip_rect(buf, 0, content_y, sw - 1, bottom - 1);

            if (items.empty())
            {
                draw_text_center(buf, sw / 2, content_y + content_h / 2 - th / 2,
                                 theme().text_disabled, "No downloads");
            }
            else
            {
                int iy = content_y - s_scroll_y;

                for (size_t i = 0; i < items.size(); ++i)
                {
                    // Skip items fully above or below visible area
                    if (iy + item_h < content_y)
                    {
                        iy += item_h;
                        continue;
                    }
                    if (iy >= bottom)
                        break;

                    const DownloadItem &it = items[i];

                    // Row background (alternate subtle shading)
                    if (i % 2 == 0)
                        rectfill(buf, 0, iy, sw - 1, iy + item_h - 1, theme().bg);
                    else
                        rectfill(buf, 0, iy, sw - 1, iy + item_h - 1, theme().surface);

                    // Title
                    draw_text(buf, pad, iy + 8, theme().text_bright, it.title.c_str());

                    // Status string and color
                    const char *status_str = "Queued";
                    int status_color = theme().text_dim;
                    switch (it.status)
                    {
                    case DownloadStatus::Downloading:
                        status_str = "Downloading";
                        status_color = theme().primary;
                        break;
                    case DownloadStatus::Extracting:
                        status_str = "Extracting";
                        status_color = theme().warning;
                        break;
                    case DownloadStatus::Complete:
                        status_str = "Complete";
                        status_color = theme().success;
                        break;
                    case DownloadStatus::Failed:
                        status_str = "Failed";
                        status_color = theme().error;
                        break;
                    default:
                        break;
                    }

                    // Status line with byte counts
                    char info[128];
                    if (it.status == DownloadStatus::Downloading && it.total > 0)
                    {
                        char recv_s[32], total_s[32];
                        format_bytes(it.received, recv_s, sizeof(recv_s));
                        format_bytes(it.total, total_s, sizeof(total_s));
                        sprintf(info, "%s  -  %s / %s  (%d%%)",
                                status_str, recv_s, total_s,
                                (int)(it.progress * 100));
                    }
                    else if (it.status == DownloadStatus::Failed && !it.error.empty())
                        sprintf(info, "%s: %s", status_str, it.error.c_str());
                    else
                        sprintf(info, "%s", status_str);

                    draw_text(buf, pad, iy + 8 + th + 4, status_color, info);

                    // Progress bar for active downloads
                    if (it.status == DownloadStatus::Downloading ||
                        it.status == DownloadStatus::Extracting)
                    {
                        int bar_x = pad;
                        int bar_w = sw - pad * 2;
                        int bar_y = iy + item_h - bar_h - 4;
                        rectfill(buf, bar_x, bar_y, bar_x + bar_w - 1, bar_y + bar_h - 1,
                                 theme().panel);
                        int fill = (int)(it.progress * bar_w);
                        if (fill > 0)
                        {
                            int bar_color = (it.status == DownloadStatus::Extracting)
                                                ? theme().warning
                                                : theme().primary;
                            rectfill(buf, bar_x, bar_y, bar_x + fill - 1, bar_y + bar_h - 1,
                                     bar_color);
                        }
                    }

                    // Divider between items
                    if (i + 1 < items.size())
                        hline(buf, pad, iy + item_h - 1, sw - pad, theme().divider);

                    iy += item_h;
                }
            }

            set_clip_rect(buf, 0, 0, sw - 1, sh - 1);

            // --- Back navigation ---
            if (back_btn.clicked)
                app.switch_screen(Screen::Library);
        }

    } // namespace ui
} // namespace launcher
