// screen_server_select.cpp — step one of signing in.
//
// Two ways to name a server: type an address, or pick one off the LAN. Both
// end in the same place, a probe that decides whether something at that
// address is actually a LANCommander server.
//
// The interesting part is how the probe stays responsive without threads. A
// bare hostname expands to eight candidate URLs (see suggest_probe_uris), and
// each one can block for up to the HTTP timeout. Probing all eight inline
// would freeze the window for most of a minute with nothing on screen.
//
// Instead the whole screen is drawn first — including the progress text and a
// Cancel button — and then exactly ONE candidate is probed as the last thing
// the frame does. The window repaints between every candidate, so the user
// sees which address is being tried and can stop at eight different points
// rather than none.

#include "ui/screen_server_select.h"
#include "ui/auth_background.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"
#include "app/app.h"
#include "app/logger.h"

#include <lancommander/clients/beacon_client.h>
#include <lancommander/util/uri.h>

#include <cstdio>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            bool s_initialized = false;
            std::string s_address;
            TextEditState s_address_edit;
            std::string s_error;

            // Probe state. index < 0 means idle.
            std::vector<std::string> s_candidates;
            int s_candidate_index = -1;

            // Beacon results, refreshed while the screen is open.
            std::vector<lancommander::DiscoveredServer> s_discovered;
            bool s_scanned = false;
            bool s_scanning = false;

            ScrollState s_list_scroll;

            // Per-candidate HTTP cap. The .NET client uses 5s; without this
            // the C++ side inherits the WinINet default, which on a network
            // that silently drops packets is far longer.
            const int PROBE_TIMEOUT_MS = 5000;

            // The beacon blocks for its whole timeout, so it is kept short and
            // re-issued rather than run once for 20 seconds.
            const int BEACON_TIMEOUT_MS = 1500;

            void begin_probe(const std::string &address)
            {
                s_error.clear();
                s_candidates = lancommander::suggest_probe_uris(address);
                s_candidate_index = s_candidates.empty() ? -1 : 0;

                if (s_candidates.empty())
                    s_error = "Enter a server address";
            }

            void cancel_probe()
            {
                s_candidates.clear();
                s_candidate_index = -1;
            }
        } // namespace

        void screen_server_select_reset()
        {
            s_initialized = false;
            cancel_probe();
            s_error.clear();
        }

        void screen_server_select_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();

            if (!s_initialized)
            {
                s_address = app.settings().authentication.server_address;
                s_address_edit = TextEditState();
                s_address_edit.caret = (int)s_address.size();
                s_address_edit.sel_anchor = s_address_edit.caret;
                s_initialized = true;
            }

            auth_background_draw(buf, sw, sh);

            const bool probing = (s_candidate_index >= 0);

            // ---------------------------------------------------------------
            // Panel
            // ---------------------------------------------------------------
            const int top = chrome_height();
            const int panel_w = 380;
            const int panel_h = 320;
            const int px = (sw - panel_w) / 2;
            const int py = top + (sh - top - panel_h) / 2;
            const int th = text_height();

            panel(buf, px, py, panel_w, panel_h, theme().panel);

            const int cx = px + panel_w / 2;
            int y = py + 18;

            // Same wordmark and the same reasoning as the login card; see
            // screen_login.cpp. ServerSelectionView opens with the logo too.
            const int logo_w = (panel_w - 48) * 7 / 8;
            y += auth_logo_draw(buf, cx, y, logo_w) + 16;

            const int field_x = px + 24;
            const int field_w = panel_w - 48;
            const int field_h = 22;

            label(buf, field_x, y, theme().text_dim, "Server Address");
            y += th + 4;

            // While probing the field is inert: editing the address mid-probe
            // would leave the candidate list describing something else.
            TextInputState addr = text_input(buf, field_x, y, field_w, field_h,
                                             s_address, 256, !probing,
                                             probing ? input_blocked(input) : input,
                                             s_address_edit);
            y += field_h + 6;

            draw_text(buf, field_x, y, theme().text_disabled,
                      "e.g. lancommander, 192.168.1.10:1337");
            y += th + 10;

            // ---------------------------------------------------------------
            // Connect / Cancel
            // ---------------------------------------------------------------
            const int btn_w = 110;
            // ServerSelectionView gives Connect Padding="12" — taller than
            // the default button, near enough the Large metric.
            const int btn_h = button_height_large();
            const int btn_x = px + panel_w - 24 - btn_w;

            bool start_requested = false;

            if (probing)
            {
                if (button(buf, btn_x, y, btn_w, btn_h, "Cancel", input).clicked ||
                    input.key_pressed(Key::Escape))
                {
                    log_info("Server probe cancelled by user");
                    cancel_probe();
                }
            }
            else
            {
                if (button(buf, btn_x, y, btn_w, btn_h, "Connect", input,
                           ButtonStyle::Primary).clicked ||
                    addr.submitted)
                    start_requested = true;
            }

            y += btn_h + 10;

            // ---------------------------------------------------------------
            // Progress / error
            // ---------------------------------------------------------------
            if (probing)
            {
                char msg[320];
                std::sprintf(msg, "Trying %d of %d: %s",
                             s_candidate_index + 1, (int)s_candidates.size(),
                             s_candidates[s_candidate_index].c_str());
                draw_text(buf, field_x, y, theme().text_dim, msg);
                y += th + 6;

                // Progress bar over the candidate list.
                const int bar_w = field_w;
                const int bar_h = 4;
                const int filled = bar_w * s_candidate_index / (int)s_candidates.size();
                gfx::fill_rect(buf, gfx::rect(field_x, y, bar_w, bar_h), theme().surface);
                gfx::fill_rect(buf, gfx::rect(field_x, y, filled, bar_h), theme().primary);
                y += bar_h + 10;
            }
            else if (!s_error.empty())
            {
                draw_text(buf, field_x, y, theme().error, s_error.c_str());
                y += th + 6;
            }
            else
            {
                y += th + 6;
            }

            // ---------------------------------------------------------------
            // Discovered servers
            // ---------------------------------------------------------------
            divider(buf, field_x, y, field_w);
            y += 8;

            // ServerSelectionView draws this caption at 12 with Opacity 0.6,
            // which is the Small rung against the secondary text colour — not
            // the disabled one.
            draw_text(buf, field_x, y, theme().text_dim,
                      s_scanning ? "Scanning..." : "Discovered Servers",
                      FontSize::Small);

            {
                const int rescan_w = 70;
                if (button(buf, px + panel_w - 24 - rescan_w, y - 4, rescan_w, 20,
                           "Rescan", probing ? input_blocked(input) : input).clicked)
                    s_scanned = false;
            }
            y += th + 6;

            const int list_h = py + panel_h - 16 - y;
            const int row_h = th + 10;

            if (s_discovered.empty())
            {
                draw_text(buf, field_x, y, theme().text_disabled,
                          s_scanned ? "No servers found on this network" : "");
            }
            else
            {
                gfx::push_clip(buf, gfx::rect(field_x, y, field_w, list_h));

                for (size_t i = 0; i < s_discovered.size(); ++i)
                {
                    const int ry = y + (int)i * row_h - s_list_scroll.offset;
                    if (ry + row_h < y || ry > y + list_h)
                        continue;

                    const bool hovered = !probing &&
                                         input.mouse.x >= field_x &&
                                         input.mouse.x < field_x + field_w &&
                                         input.mouse.y >= ry &&
                                         input.mouse.y < ry + row_h;

                    if (hovered)
                        gfx::fill_rect(buf, gfx::rect(field_x, ry, field_w, row_h),
                                       theme().panel_hover);

                    const lancommander::DiscoveredServer &d = s_discovered[i];
                    draw_text(buf, field_x + 6, ry + 5, theme().text,
                              d.name.empty() ? "LANCommander" : d.name.c_str());
                    // Same treatment as the caption above: 12 at 0.6 opacity
                    // in the Avalonia list, so Small and secondary. It was
                    // drawing at the disabled colour, which after that token
                    // dropped to Avalonia's real 25%-white value is barely
                    // legible on the card.
                    draw_text_right(buf, field_x + field_w - 6, ry + 5,
                                    theme().text_dim, d.address.c_str(),
                                    FontSize::Small);

                    // Picking a server only fills the box, matching the
                    // Avalonia flow: the user still confirms with Connect, and
                    // the address is probed the same way a typed one is.
                    if (hovered && input.mouse.clicked)
                    {
                        s_address = d.address;
                        s_address_edit = TextEditState();
                        s_address_edit.caret = (int)s_address.size();
                        s_address_edit.sel_anchor = s_address_edit.caret;
                    }
                }

                gfx::pop_clip(buf);

                const int content_h = (int)s_discovered.size() * row_h;
                if (input.mouse.wheel_delta != 0 && !probing &&
                    input.mouse.y >= y && input.mouse.y < y + list_h)
                {
                    s_list_scroll.offset -= input.mouse.wheel_delta * row_h;
                    if (s_list_scroll.offset < 0) s_list_scroll.offset = 0;
                    const int max_scroll = content_h - list_h;
                    if (s_list_scroll.offset > (max_scroll > 0 ? max_scroll : 0))
                        s_list_scroll.offset = (max_scroll > 0 ? max_scroll : 0);
                }

                scrollbar(buf, field_x + field_w - 12, y, list_h,
                          content_h, list_h, s_list_scroll,
                          probing ? input_blocked(input) : input);
            }

            // ===============================================================
            // Blocking work, deliberately last
            // ===============================================================
            //
            // Everything above has already been composited, so whatever we
            // block on below is on screen before it happens.

            if (start_requested)
            {
                begin_probe(s_address);
                gfx::present();   // show "Trying 1 of N" before the first stall
            }

            if (s_candidate_index >= 0 &&
                s_candidate_index < (int)s_candidates.size())
            {
                app.http().set_timeout_ms(PROBE_TIMEOUT_MS, PROBE_TIMEOUT_MS);

                const std::string candidate = s_candidates[s_candidate_index];
                auto result = app.connection().probe_candidate(candidate);

                if (result && result.value)
                {
                    log_info("Server found at %s", candidate.c_str());

                    // Persist the RESOLVED address, not what was typed.
                    app.connection().set_server_address(candidate);
                    app.settings().authentication.server_address = candidate;

                    cancel_probe();
                    app.switch_screen(Screen::Login);
                    return;
                }

                ++s_candidate_index;

                if (s_candidate_index >= (int)s_candidates.size())
                {
                    log_warn("No LANCommander server found for '%s'", s_address.c_str());
                    s_error = "Could not find a server at that address";
                    cancel_probe();
                }
            }

            // One short beacon sweep per visit, plus whenever Rescan is
            // pressed. Kept out of the probe path so the two cannot block each
            // other in the same frame.
            if (!s_scanned && !probing)
            {
                s_scanning = true;
                gfx::present();

                lancommander::BeaconClient beacon;
                auto found = beacon.discover(BEACON_TIMEOUT_MS);
                if (found)
                {
                    s_discovered = found.value;
                    log_info("Beacon sweep found %d server(s)", (int)s_discovered.size());
                }
                else
                {
                    log_warn("Beacon sweep failed: %s", found.error.c_str());
                }

                s_scanned = true;
                s_scanning = false;
            }
        }

    } // namespace ui
} // namespace launcher
