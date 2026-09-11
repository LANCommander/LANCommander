#include "ui/screen_script_console.h"

#include "ui/icons.h"
#include "ui/theme.h"
#include "ui/widgets.h"
#include "ui/window_chrome.h"

#include "app/app.h"
#include "app/script_host.h"

#include "gfx/gfx.h"

#include <cstdio>
#include <cstring>
#include <string>
#include <utility>
#include <vector>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            // Selected run, by id rather than index: the host drops its oldest
            // records, so an index means a different run a minute later.
            unsigned long s_selected_run = 0;

            ScrollState s_run_scroll;
            ScrollState s_out_scroll;
            ScrollState s_src_scroll;

            // The console has two panes for the same run and they answer
            // different questions, so it is a switch rather than a split: on
            // an 800x600 window -- which is what a Win9x machine has -- half
            // of each is too little of either.
            enum class Pane
            {
                Output,
                Source,
                Breaks
            };
            Pane s_pane = Pane::Output;

            Pane next_pane(Pane p)
            {
                switch (p)
                {
                case Pane::Output: return Pane::Source;
                case Pane::Source: return Pane::Breaks;
                default:           return Pane::Output;
                }
            }

            const char *pane_label(Pane p)
            {
                switch (p)
                {
                case Pane::Output: return "Output";
                case Pane::Source: return "Source";
                default:           return "Breaks";
                }
            }

            ScrollState s_bp_scroll;

            // Stick to the bottom until the user scrolls up, which is what a
            // console has to do to be readable while output is arriving.
            bool s_follow = true;
            unsigned long s_last_line_count = 0;

            // The tail strip over other screens.
            bool s_tail_pinned = false;
            unsigned long s_tail_seen_lines = 0;
            unsigned int s_tail_hide_at = 0; // ticks; 0 = not counting down

            // Source of the selected run, cached so the file is not re-read
            // every frame.
            unsigned long s_source_run = 0;
            std::vector<std::string> s_source;

            const char *status_text(const ScriptRunInfo &run)
            {
                if (run.active)
                    return "running";
                if (run.skipped_runtime)
                    return "skipped";
                if (!run.ran)
                    return "no script";
                return run.success ? "ok" : "failed";
            }

            gfx::Color status_color(const ScriptRunInfo &run)
            {
                if (run.active)
                    return theme().primary;
                if (!run.ran)
                    return theme().text_disabled;
                return run.success ? theme().success : theme().error;
            }

            gfx::Color line_color(ScriptLineKind kind)
            {
                switch (kind)
                {
                case ScriptLineKind::Err:  return theme().warning;
                case ScriptLineKind::Fail: return theme().error;
                case ScriptLineKind::Note: return theme().text_dim;
                default:                   return theme().text;
                }
            }

            // "Install.ps1" from a full path. Breakpoints are keyed on this
            // because it is what the interpreter calls the script, and it
            // survives the game being reinstalled somewhere else.
            std::string script_name_of(const std::string &path)
            {
                std::string::size_type cut = path.find_last_of("/\\");
                return cut == std::string::npos ? path : path.substr(cut + 1);
            }

            void read_source(const std::string &path, std::vector<std::string> *out)
            {
                out->clear();

                FILE *f = fopen(path.c_str(), "rb");
                if (!f)
                    return;

                std::string all;
                char buf[4096];
                size_t got;
                while ((got = fread(buf, 1, sizeof(buf), f)) > 0)
                    all.append(buf, got);
                fclose(f);

                std::string line;
                for (size_t i = 0; i < all.size(); ++i)
                {
                    if (all[i] == '\n')
                    {
                        if (!line.empty() && line[line.size() - 1] == '\r')
                            line.erase(line.size() - 1);
                        out->push_back(line);
                        line.clear();
                    }
                    else
                    {
                        line += all[i];
                    }
                }
                if (!line.empty())
                    out->push_back(line);
            }

            // Copies out what the panes need, so the host's lock is held for
            // one short stretch rather than across the whole draw.
            struct Snapshot
            {
                struct Row
                {
                    unsigned long id;
                    std::string title;
                    std::string type_name;
                    std::string status;
                    gfx::Color color;
                    bool active;
                };

                std::vector<Row> rows;
                std::vector<ScriptConsoleLine> lines; // of the selected run
                std::string script_path;
                std::string error;
                std::string return_value;
                bool have_selection;
                bool trimmed;
                bool active;
                int exit_code;
                unsigned int duration_ms;

                Snapshot()
                    : have_selection(false), trimmed(false), active(false),
                      exit_code(0), duration_ms(0) {}
            };

            void snapshot(const ScriptHost &host, unsigned long selected,
                          Snapshot *out)
            {
                host.lock_log();

                const std::vector<ScriptRunInfo> &runs = host.runs();

                for (size_t i = 0; i < runs.size(); ++i)
                {
                    Snapshot::Row row;
                    row.id = runs[i].id;
                    row.title = runs[i].title;
                    row.type_name = runs[i].type_name;
                    row.status = status_text(runs[i]);
                    row.color = status_color(runs[i]);
                    row.active = runs[i].active;
                    out->rows.push_back(row);

                    if (runs[i].id == selected)
                    {
                        out->have_selection = true;
                        out->lines = runs[i].lines;
                        out->script_path = runs[i].script_path;
                        out->error = runs[i].error;
                        out->return_value = runs[i].return_value;
                        out->trimmed = runs[i].lines_trimmed;
                        out->active = runs[i].active;
                        out->exit_code = runs[i].exit_code;
                        out->duration_ms = runs[i].duration_ms;
                    }
                }

                host.unlock_log();
            }

            // The most recent run, which is what the console should be showing
            // when it is opened or when something new starts.
            unsigned long newest_run(const Snapshot &snap)
            {
                return snap.rows.empty() ? 0 : snap.rows[snap.rows.size() - 1].id;
            }

            void clamp_scroll(ScrollState *state, int content_h, int viewport_h)
            {
                int max_scroll = content_h - viewport_h;
                if (max_scroll < 0)
                    max_scroll = 0;
                if (state->offset > max_scroll)
                    state->offset = max_scroll;
                if (state->offset < 0)
                    state->offset = 0;
            }

            // Wheel handling shared by every pane here. Returns true when the
            // user actually scrolled, which the output pane uses to decide it
            // should stop following the tail.
            bool wheel_scroll(const InputState &input, const gfx::Rect &area,
                              ScrollState *state, int content_h, int viewport_h)
            {
                if (input.mouse.wheel_delta == 0)
                    return false;
                if (input.mouse.x < area.x || input.mouse.x >= area.x + area.w)
                    return false;
                if (input.mouse.y < area.y || input.mouse.y >= area.y + area.h)
                    return false;

                state->offset -= input.mouse.wheel_delta * 28;
                clamp_scroll(state, content_h, viewport_h);

                return true;
            }
        } // namespace

        // -------------------------------------------------------------------
        // Console screen
        // -------------------------------------------------------------------

        void screen_script_console_draw(App &app, const InputState &input)
        {
            gfx::Surface *buf = app.backbuffer();
            ScriptHost &host = app.script_host();

            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int top = chrome_height();
            const int bottom = sh - footer_height();
            const int th = text_height();
            const int pad = 12;

            Snapshot snap;
            snapshot(host, s_selected_run, &snap);

            // Nothing selected, or the selection aged out of the bounded
            // history: fall back to the newest run rather than an empty pane.
            if (!snap.have_selection)
            {
                s_selected_run = newest_run(snap);
                snap = Snapshot();
                snapshot(host, s_selected_run, &snap);
            }

            // --- Header ---
            const int header_h = 40;
            panel(buf, 0, top, sw, header_h, theme().surface);
            gfx::hline(buf, 0, top + header_h - 1, sw, theme().divider);

            const int btn_h = button_height();
            const int btn_y = top + (header_h - btn_h) / 2;

            ButtonState back = icon_button(buf, pad, btn_y,
                                           button_width("Back", Icon::ArrowLeft),
                                           btn_h, Icon::ArrowLeft, "Back", input);
            draw_text(buf, pad + 72, top + (header_h - th) / 2, theme().text_bright,
                      "Script Console");

            // Right-aligned toolbar.
            //
            // Laid out right to left, and a button that will not fit is simply
            // not drawn. The launcher's default window is 800x600 and the
            // Win9x and DOS targets are 640x480 or less, so "does the header
            // fit" is a real question rather than a theoretical one -- the
            // first version of this used labels like "Break on entry: off" and
            // ran straight off the right edge. Labels are short for the same
            // reason.
            //
            // Order matters: the rightmost entries here are the last to be
            // dropped, so Clear and the pane switch survive the narrowest
            // window.
            {
                struct Entry
                {
                    char label[24];
                    int action; // 0 clear, 1 pane, 2 debug, 3 entry, 5 re-run
                };

                Entry entries[5];
                int count = 0;

                sprintf(entries[count].label, "Clear");
                entries[count++].action = 0;

                // The tightest loop the debugger has: edit the .ps1, run it
                // again, without reinstalling the game to make its install
                // script fire. Offered only when the interpreter is free --
                // this runs on the drawing thread and would otherwise block
                // behind whatever is already running.
                if (snap.have_selection && !host.busy())
                {
                    sprintf(entries[count].label, "Run again");
                    entries[count++].action = 5;
                }

                sprintf(entries[count].label, "%s", pane_label(next_pane(s_pane)));
                entries[count++].action = 1;

                sprintf(entries[count].label, "Dbg: %s",
                        app.script_debugging() ? "on" : "off");
                entries[count++].action = 2;

                // Stopping on the first statement is the only way to debug a
                // script the launcher starts on its own -- an install begins
                // and ends before there is any moment to press anything.
                sprintf(entries[count].label, "Entry: %s",
                        host.break_on_entry() ? "on" : "off");
                entries[count++].action = 3;


                // Never over the title.
                const int floor_x = pad + 72 + text_width("Script Console") + 12;

                int tx = sw - pad;

                for (int i = 0; i < count; ++i)
                {
                    const int w = button_width(entries[i].label);

                    if (tx - w < floor_x)
                        break;

                    tx -= w;

                    if (button(buf, tx, btn_y, w, btn_h, entries[i].label, input)
                            .clicked)
                    {
                        switch (entries[i].action)
                        {
                        case 0:
                            host.clear_log();
                            s_selected_run = 0;
                            s_source_run = 0;
                            s_source.clear();
                            break;
                        case 1:
                            s_pane = next_pane(s_pane);
                            break;
                        case 2:
                            // Through App, not the host: this is the persisted
                            // Debug.EnableScriptDebugging setting, and the
                            // Settings screen's checkbox is the same value.
                            app.set_script_debugging(!app.script_debugging());
                            break;
                        case 3:
                            host.set_break_on_entry(!host.break_on_entry());
                            break;
                        case 5:
                            // Blocks this thread for the length of the script.
                            // Acceptable because it is an explicit debugging
                            // action, and because a script paused at a
                            // breakpoint pumps frames from inside the run.
                            host.run_again(s_selected_run, NULL);
                            s_selected_run = 0; // select whatever it produced
                            s_follow = true;
                            break;
                        default:
                            break;
                        }
                    }

                    tx -= 8;
                }
            }

            // --- Layout ---
            const int content_y = top + header_h;
            const int content_h = bottom - content_y;
            const int list_w = 210;
            const int row_h = 34;

            // --- Run list ---
            panel(buf, 0, content_y, list_w, content_h, theme().bg);
            gfx::vline(buf, list_w, content_y, content_h, theme().divider);

            const gfx::Rect list_area = gfx::rect(0, content_y, list_w, content_h);
            const int list_content_h = (int)snap.rows.size() * row_h;
            wheel_scroll(input, list_area, &s_run_scroll, list_content_h, content_h);
            clamp_scroll(&s_run_scroll, list_content_h, content_h);

            gfx::push_clip(buf, list_area);

            if (snap.rows.empty())
            {
                draw_text_wrap(buf, pad, content_y + 16, list_w - pad * 2,
                               theme().text_disabled,
                               "No scripts have run yet. Install a game, or "
                               "start one, and its output appears here.");
            }

            for (size_t i = 0; i < snap.rows.size(); ++i)
            {
                const int ry = content_y + (int)i * row_h - s_run_scroll.offset;
                if (ry + row_h < content_y || ry >= bottom)
                    continue;

                const Snapshot::Row &row = snap.rows[i];
                const bool selected = (row.id == s_selected_run);
                const gfx::Rect r = gfx::rect(0, ry, list_w, row_h);
                const bool hovered = gfx::rect_contains(r, input.mouse.x, input.mouse.y);

                if (selected)
                    gfx::fill_rect(buf, r, theme().panel);
                else if (hovered)
                    gfx::fill_rect(buf, r, theme().panel_hover);

                if (hovered && input.mouse.clicked)
                {
                    s_selected_run = row.id;
                    s_out_scroll.offset = 0;
                    s_src_scroll.offset = 0;
                    s_follow = true;
                }

                draw_text(buf, pad, ry + 4, theme().text_bright, row.type_name.c_str());
                draw_text_right(buf, list_w - pad, ry + 4, row.color, row.status.c_str());
                draw_text(buf, pad, ry + 4 + th + 1, theme().text_dim,
                          row.title.c_str());
            }

            gfx::pop_clip(buf);

            if (list_content_h > content_h)
            {
                scrollbar(buf, list_w - 12, content_y, content_h,
                          list_content_h, content_h, s_run_scroll, input);
            }

            // --- Detail pane ---
            const int px = list_w + 1;
            const int pw = sw - px;
            int py = content_y;

            if (!snap.have_selection)
            {
                draw_text_center(buf, px + pw / 2, content_y + content_h / 2 - th / 2,
                                 theme().text_disabled, "Select a run");
                if (back.clicked)
                    app.go_back();
                return;
            }

            // A one-line summary above whichever pane is showing, so the exit
            // code is visible without hunting for it in the output.
            {
                char summary[256];
                sprintf(summary, "exit %d  -  %ums", snap.exit_code, snap.duration_ms);
                draw_text(buf, px + pad, py + 4, theme().text_dim, summary);

                if (!snap.return_value.empty())
                {
                    const std::string ret = "$Return = " + snap.return_value;
                    draw_text(buf, px + pad + 160, py + 4, theme().text_dim,
                              ret.c_str());
                }

                if (!snap.error.empty())
                {
                    draw_text_right(buf, sw - pad, py + 4, theme().error,
                                    snap.error.c_str());
                }

                py += th + 8;
                gfx::hline(buf, px, py, pw, theme().divider);
                py += 1;
            }

            const int pane_h = bottom - py;
            const gfx::Rect pane = gfx::rect(px, py, pw, pane_h);
            const int line_h = th + 2;

            if (s_pane == Pane::Output)
            {
                const int out_content_h = (int)snap.lines.size() * line_h +
                                          (snap.trimmed ? line_h : 0);

                if (wheel_scroll(input, pane, &s_out_scroll, out_content_h, pane_h))
                {
                    // Scrolling up is the user saying "hold still"; scrolling
                    // back to the bottom resumes following.
                    s_follow = (s_out_scroll.offset >= out_content_h - pane_h);
                }

                // Only when new output arrived, so the pane does not fight a
                // scrollbar drag.
                const unsigned long now_lines = host.line_counter();
                if (s_follow && now_lines != s_last_line_count)
                {
                    s_out_scroll.offset = out_content_h - pane_h;
                    s_last_line_count = now_lines;
                }
                clamp_scroll(&s_out_scroll, out_content_h, pane_h);

                gfx::push_clip(buf, pane);

                int ly = py - s_out_scroll.offset;

                if (snap.trimmed)
                {
                    draw_text(buf, px + pad, ly, theme().text_disabled,
                              "[earlier output dropped - the full text is in the "
                              "launcher log]");
                    ly += line_h;
                }

                for (size_t i = 0; i < snap.lines.size(); ++i)
                {
                    if (ly + line_h >= py && ly < bottom)
                    {
                        draw_text(buf, px + pad, ly, line_color(snap.lines[i].kind),
                                  snap.lines[i].text.c_str());
                    }
                    ly += line_h;
                }

                if (snap.lines.empty() && !snap.trimmed)
                {
                    draw_text(buf, px + pad, py + 8, theme().text_disabled,
                              "(the script produced no output)");
                }

                gfx::pop_clip(buf);

                if (out_content_h > pane_h)
                {
                    scrollbar(buf, sw - 12, py, pane_h, out_content_h, pane_h,
                              s_out_scroll, input);
                }
            }
            else if (s_pane == Pane::Source)
            {
                // --- Source, with a breakpoint gutter ---
                if (s_source_run != s_selected_run)
                {
                    read_source(snap.script_path, &s_source);
                    s_source_run = s_selected_run;
                }

                const int src_content_h = (int)s_source.size() * line_h;
                const int gutter_w = 46;

                wheel_scroll(input, pane, &s_src_scroll, src_content_h, pane_h);
                clamp_scroll(&s_src_scroll, src_content_h, pane_h);

                gfx::push_clip(buf, pane);

                if (s_source.empty())
                {
                    draw_text_wrap(buf, px + pad, py + 8, pw - pad * 2,
                                   theme().text_disabled,
                                   snap.script_path.empty()
                                       ? "This run had no script file."
                                       : ("Could not read " + snap.script_path).c_str());
                }

                for (size_t i = 0; i < s_source.size(); ++i)
                {
                    const int ly = py + (int)i * line_h - s_src_scroll.offset;
                    if (ly + line_h < py || ly >= bottom)
                        continue;

                    const int line_no = (int)i + 1;
                    const bool has_bp =
                        host.has_breakpoint(snap.script_path, line_no);

                    // The gutter is the click target, not the whole row: a
                    // click on the code itself should not arm a breakpoint the
                    // user cannot see they armed.
                    const gfx::Rect gutter =
                        gfx::rect(px, ly, gutter_w, line_h);
                    const bool over_gutter =
                        gfx::rect_contains(gutter, input.mouse.x, input.mouse.y);

                    if (over_gutter)
                        gfx::fill_rect(buf, gutter, theme().panel_hover);

                    if (over_gutter && input.mouse.clicked)
                        host.toggle_breakpoint(snap.script_path, line_no);

                    if (has_bp)
                    {
                        fill_rounded_rect(buf, gfx::rect(px + 4, ly + 2, 8, 8), 4,
                                          theme().error);
                    }

                    char num[16];
                    sprintf(num, "%d", line_no);
                    draw_text_right(buf, px + gutter_w - 6, ly,
                                    has_bp ? theme().text_bright : theme().text_disabled,
                                    num);

                    draw_text(buf, px + gutter_w + 4, ly, theme().text,
                              s_source[i].c_str());
                }

                gfx::pop_clip(buf);

                if (src_content_h > pane_h)
                {
                    scrollbar(buf, sw - 12, py, pane_h, src_content_h, pane_h,
                              s_src_scroll, input);
                }

            }
            else
            {
                // --- Breakpoints ---
                //
                // Armed lines live in the host, not in whichever source pane
                // happens to be open, so without a list of them there is no way
                // to find a breakpoint you set in a script you have navigated
                // away from -- or to notice the one still armed in a game you
                // stopped debugging an hour ago.
                std::vector<ScriptHost::BreakpointRef> refs;
                host.breakpoints(&refs);

                const int row_h = th * 2 + 10;
                const int bp_content_h = (int)refs.size() * row_h;

                wheel_scroll(input, pane, &s_bp_scroll, bp_content_h, pane_h);
                clamp_scroll(&s_bp_scroll, bp_content_h, pane_h);

                gfx::push_clip(buf, pane);

                if (refs.empty())
                {
                    draw_text_wrap(buf, px + pad, py + 10, pw - pad * 2,
                                   theme().text_disabled,
                                   "No breakpoints. Open a run's Source pane and "
                                   "click the line-number gutter to set one, or "
                                   "turn on Entry to stop at the first statement "
                                   "of the next script that runs.");
                }

                for (size_t i = 0; i < refs.size(); ++i)
                {
                    const int ry = py + (int)i * row_h - s_bp_scroll.offset;
                    if (ry + row_h < py || ry >= bottom)
                        continue;

                    const gfx::Rect r = gfx::rect(px, ry, pw, row_h);
                    if (gfx::rect_contains(r, input.mouse.x, input.mouse.y))
                        gfx::fill_rect(buf, r, theme().panel_hover);

                    fill_rounded_rect(buf, gfx::rect(px + pad, ry + 6, 8, 8), 4,
                                      theme().error);

                    const char *remove = "Remove";
                    const int rw = button_width(remove);

                    // Clipped to stop short of the Remove button. An install
                    // path is longer than any pane, and left to itself it runs
                    // straight under the button and off the edge.
                    const int text_x = px + pad + 16;
                    const int text_w = (sw - pad - rw - 8) - text_x;

                    gfx::push_clip(buf, gfx::rect(text_x, ry, text_w > 0 ? text_w : 1,
                                                  row_h));

                    char label[128];
                    sprintf(label, "%s : %d", refs[i].name.c_str(), refs[i].line);
                    draw_text(buf, text_x, ry + 4, theme().text_bright, label);

                    // The path is what actually distinguishes two breakpoints
                    // that are both "Install.ps1 : 12".
                    draw_text(buf, text_x, ry + 4 + th + 1, theme().text_disabled,
                              refs[i].script_path.c_str());

                    gfx::pop_clip(buf);

                    if (button(buf, sw - pad - rw, ry + (row_h - btn_h) / 2, rw,
                               btn_h, remove, input)
                            .clicked)
                    {
                        host.toggle_breakpoint(refs[i].script_path, refs[i].line);
                    }
                }

                gfx::pop_clip(buf);

                if (bp_content_h > pane_h)
                {
                    scrollbar(buf, sw - 12, py, pane_h, bp_content_h, pane_h,
                              s_bp_scroll, input);
                }
            }

            // A breakpoint with the debugger off would silently never fire,
            // which is the worst possible outcome for someone who just set one.
            // Say so on every pane, and offer the switch -- it is as easy to
            // arm a breakpoint from the Source pane as to forget the toggle.
            if (host.breakpoint_count() > 0 && !host.debug_enabled())
            {
                const int warn_h = 26;
                const gfx::Rect warn = gfx::rect(px, bottom - warn_h, pw, warn_h);
                gfx::fill_rect(buf, warn, theme().panel);
                draw_text(buf, px + pad, bottom - warn_h + 5, theme().warning,
                          "Breakpoints are set but debugging is off.");

                const char *label = "Turn on";
                const int w = button_width(label);
                if (button(buf, sw - pad - w, bottom - warn_h + 2, w, btn_h,
                           label, input)
                        .clicked)
                {
                    app.set_script_debugging(true);
                }
            }

            if (back.clicked)
                app.go_back();
        }

        // -------------------------------------------------------------------
        // Tail strip
        // -------------------------------------------------------------------

        void script_tail_draw(App &app, const InputState &input)
        {
            ScriptHost &host = app.script_host();

            // Never over the console -- it is already showing all of this, and
            // a strip on top of it would cover the newest output.
            if (app.current_screen() == Screen::ScriptConsole)
                return;

            const unsigned long lines = host.line_counter();
            const bool running = host.busy();

            // Appear on the first output of a run, stay for a few seconds after
            // the last, and stay indefinitely while the user has it pinned.
            if (lines != s_tail_seen_lines)
            {
                s_tail_seen_lines = lines;
                s_tail_hide_at = gfx::ticks_ms() + 6000;
            }

            const bool timed_out =
                (s_tail_hide_at == 0) || (gfx::ticks_ms() > s_tail_hide_at);

            if (!s_tail_pinned && !running && timed_out)
                return;

            Snapshot snap;
            unsigned long newest = 0;
            {
                Snapshot probe;
                snapshot(host, 0, &probe);
                newest = newest_run(probe);
            }
            snapshot(host, newest, &snap);

            if (!snap.have_selection)
                return;

            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int th = text_height();
            const int line_h = th + 2;
            const int shown = 5;
            const int strip_h = line_h * shown + 30;
            const int y = sh - footer_height() - strip_h;

            fill_rounded_rect_alpha(buf, gfx::rect(8, y, sw - 16, strip_h), 4,
                                    gfx::rgba(0, 0, 0, 210));

            const int btn_h = button_height();
            int tx = sw - 16;

            {
                const char *label = "Open console";
                const int w = button_width(label);
                tx -= w;
                if (button(buf, tx, y + 4, w, btn_h, label, input).clicked)
                    app.switch_screen(Screen::ScriptConsole);
                tx -= 6;
            }

            {
                const char *label = s_tail_pinned ? "Unpin" : "Pin";
                const int w = button_width(label);
                tx -= w;
                if (button(buf, tx, y + 4, w, btn_h, label, input).clicked)
                    s_tail_pinned = !s_tail_pinned;
            }

            // The heading names the run, because during a queued install this
            // strip is the only thing that says which game is being worked on.
            {
                const Snapshot::Row &row = snap.rows[snap.rows.size() - 1];
                char heading[192];
                sprintf(heading, "%s - %s [%s]", row.title.c_str(),
                        row.type_name.c_str(), row.status.c_str());
                draw_text(buf, 16, y + 6, row.color, heading);
            }

            // The last few lines, oldest at the top, which is how a tail reads.
            int first = (int)snap.lines.size() - shown;
            if (first < 0)
                first = 0;

            int ly = y + 26;
            for (size_t i = (size_t)first; i < snap.lines.size(); ++i)
            {
                draw_text(buf, 16, ly, line_color(snap.lines[i].kind),
                          snap.lines[i].text.c_str());
                ly += line_h;
            }
        }

        // -------------------------------------------------------------------
        // Paused debugger
        // -------------------------------------------------------------------

        void script_debugger_draw(App &app, const InputState &input)
        {
            ScriptHost &host = app.script_host();

            gfx::Surface *buf = app.backbuffer();
            const int sw = app.screen_width();
            const int sh = app.screen_height();
            const int th = text_height();
            const int pad = 16;
            const int line_h = th + 2;

            const ScriptPause pause = host.pause_info();

            std::vector<std::string> source;
            host.pause_source(&source);

            std::vector<std::pair<std::string, std::string> > vars;
            host.pause_variables(&vars);

            // --- Header ---
            const int header_h = 44;
            panel(buf, 0, 0, sw, header_h, theme().surface);
            gfx::hline(buf, 0, header_h - 1, sw, theme().divider);

            {
                char heading[224];
                sprintf(heading, "Paused - %s line %d", pause.script_name.c_str(),
                        pause.line);
                draw_text(buf, pad, (header_h - th) / 2, theme().text_bright, heading);
            }

            const int btn_h = button_height();
            const int btn_y = (header_h - btn_h) / 2;
            int tx = sw - pad;

            // Right to left, and anything that will not fit is dropped rather
            // than drawn off the edge -- the same rule the console header
            // follows, and for the same 640x480 reason. Abort is first so it
            // is the last to go: a debugger you cannot get out of is worse
            // than one you cannot step in.
            {
                const char *labels[4] = { "Abort", "Go (F5)", "Over (F10)",
                                          "Into (F11)" };
                const ScriptResume actions[4] = {
                    ScriptResume::Abort, ScriptResume::Go,
                    ScriptResume::StepOver, ScriptResume::StepInto
                };

                for (int i = 0; i < 4; ++i)
                {
                    const int w = button_width(labels[i]);
                    if (tx - w < pad)
                        break;

                    tx -= w;
                    if (button(buf, tx, btn_y, w, btn_h, labels[i], input).clicked)
                        host.resume(actions[i]);
                    tx -= 8;
                }
            }

            // Keys work whether or not their button had room to draw, which is
            // what makes the narrow-window case usable rather than merely
            // survivable.
            if (input.key_pressed(Key::F5))
                host.resume(ScriptResume::Go);
            if (input.key_pressed(Key::F10))
                host.resume(ScriptResume::StepOver);
            if (input.key_pressed(Key::F11))
                host.resume(ScriptResume::StepInto);
            if (input.key_pressed(Key::Escape))
                host.resume(ScriptResume::Abort);

            // F9 on the paused line, which is the shortcut every debugger has
            // and the only way to arm a breakpoint without aiming at a
            // four-pixel gutter.
            if (input.key_pressed(Key::F9))
                host.toggle_breakpoint(pause.script_path, pause.line);

            // --- Source on the left, variables on the right ---
            const int vars_w = 280;
            const int src_w = sw - vars_w - 1;
            const int body_y = header_h;
            const int body_h = sh - body_y;
            const int gutter_w = 46;

            const gfx::Rect src_pane = gfx::rect(0, body_y, src_w, body_h);
            const int src_content_h = (int)source.size() * line_h;

            // Keep the paused line in view without fighting the user: recentre
            // only when it would otherwise be off-screen.
            {
                const int want = (pause.line - 1) * line_h;
                if (want < s_src_scroll.offset ||
                    want > s_src_scroll.offset + body_h - line_h * 2)
                {
                    s_src_scroll.offset = want - body_h / 3;
                }
            }
            wheel_scroll(input, src_pane, &s_src_scroll, src_content_h, body_h);
            clamp_scroll(&s_src_scroll, src_content_h, body_h);

            gfx::push_clip(buf, src_pane);

            if (source.empty())
            {
                draw_text(buf, pad, body_y + 12, theme().text_disabled,
                          "The script's source could not be read.");
            }

            for (size_t i = 0; i < source.size(); ++i)
            {
                const int ly = body_y + (int)i * line_h - s_src_scroll.offset;
                if (ly + line_h < body_y || ly >= sh)
                    continue;

                const int line_no = (int)i + 1;
                const bool current = (line_no == pause.line);
                const bool has_bp = host.has_breakpoint(pause.script_path, line_no);

                if (current)
                {
                    gfx::fill_rect(buf, gfx::rect(0, ly, src_w, line_h),
                                   theme().panel);
                }

                const gfx::Rect gutter = gfx::rect(0, ly, gutter_w, line_h);
                if (gfx::rect_contains(gutter, input.mouse.x, input.mouse.y) &&
                    input.mouse.clicked)
                {
                    // Breakpoints can be moved while stopped, which is most of
                    // what "basic debugging" means in practice: stop once, then
                    // arm the line you actually care about.
                    host.toggle_breakpoint(pause.script_path, line_no);
                }

                if (has_bp)
                {
                    fill_rounded_rect(buf, gfx::rect(4, ly + 2, 8, 8), 4,
                                      theme().error);
                }

                char num[16];
                sprintf(num, "%d", line_no);
                draw_text_right(buf, gutter_w - 6, ly,
                                current ? theme().text_bright : theme().text_disabled,
                                num);

                draw_text(buf, gutter_w + 4, ly,
                          current ? theme().text_bright : theme().text,
                          source[i].c_str());
            }

            gfx::pop_clip(buf);

            if (src_content_h > body_h)
                scrollbar(buf, src_w - 12, body_y, body_h, src_content_h, body_h,
                          s_src_scroll, input);

            // --- Variables ---
            gfx::vline(buf, src_w, body_y, body_h, theme().divider);
            panel(buf, src_w + 1, body_y, vars_w, body_h, theme().bg);

            draw_text(buf, src_w + pad, body_y + 8, theme().text_dim, "Variables");

            int vy = body_y + 8 + th + 6;
            const int value_x = src_w + pad;

            gfx::push_clip(buf, gfx::rect(src_w + 1, vy, vars_w, sh - vy));

            for (size_t i = 0; i < vars.size(); ++i)
            {
                if (vy >= sh)
                    break;

                const std::string name = "$" + vars[i].first;
                draw_text(buf, value_x, vy, theme().text_bright, name.c_str());
                vy += line_h;

                // Values wrap: a manifest or a path is longer than the pane and
                // truncating it would hide the part that differs.
                vy += draw_text_wrap(buf, value_x + 10, vy, vars_w - pad * 2 - 10,
                                     theme().text, vars[i].second.c_str());
                vy += 6;
            }

            if (vars.empty())
            {
                draw_text(buf, value_x, vy, theme().text_disabled,
                          "(nothing in scope)");
            }

            gfx::pop_clip(buf);
        }

    } // namespace ui
} // namespace launcher
