#include "app/script_host.h"

#include "app/logger.h"
#include "app/worker.h"

#include <lancommander/clients/game_client.h>
#include <lancommander/clients/script_client.h>
#include <lancommander/http/http_client.h>
#include <lancommander/manifest_helper.h>
#include <lancommander/script/script_helper.h>
#include <lancommander/util/path.h>

#include "gfx/gfx.h" // ticks_ms, for run durations

#include <cstddef>
#include <cstdio>
#include <map>
#include <string>
#include <utility>
#include <vector>

namespace launcher
{

    // Everything that is only needed inside the .cpp, kept out of the header so
    // the console screen does not have to see the debugger's bookkeeping.
    struct ScriptHost::Impl
    {
        // Partial output, per stream, held until a newline arrives. picoposh
        // hands the sink whatever bytes a statement produced, which is not the
        // same as a line.
        std::string partial_out;
        std::string partial_err;

        // The run currently feeding the sink. Zero when nothing is running.
        unsigned long current_run;

        // What is on disk for a game, so a lifecycle run does not need the
        // network. Populated by write_scripts, and by reading the manifest a
        // previous session wrote.
        struct Cached
        {
            std::string manifest_json;
            std::vector<lancommander::Script> scripts;
        };
        std::map<std::string, Cached> cache;

        // --- debugger ------------------------------------------------------

        // Line numbers per script PATH. A set would be tidier; a sorted vector
        // is smaller and this never grows past a handful.
        std::map<std::string, std::vector<int> > breakpoints;

        // How the next statement should be treated. Over carries the depth the
        // step was requested at, so it can run past a function call instead of
        // descending into it.
        enum class StepMode
        {
            None,
            Into,
            Over
        };
        StepMode step_mode;
        int step_depth;

        bool paused;
        bool pump_on_this_thread; // the paused script owns the drawing thread
        ScriptPause pause;
        ScriptResume resume;
        std::vector<std::pair<std::string, std::string> > pause_vars;
        std::vector<std::string> pause_lines; // source of the paused script
        std::string pause_source_path;        // which file pause_lines holds

        // The .ps1 the current run resolved to. Cached here rather than looked
        // up per statement: the debug hook fires before every one of them.
        std::string current_script_path;

        Impl()
            : current_run(0), step_mode(StepMode::None), step_depth(0),
              paused(false), pump_on_this_thread(true),
              resume(ScriptResume::None) {}
    };

    namespace
    {
        // Lines of a text file, for the debugger's source view. Returns false
        // when it cannot be read, which the caller shows as an empty pane
        // rather than treating as a failure -- a script can be running fine
        // while its file is locked or gone.
        bool read_lines(const std::string &path, std::vector<std::string> *out)
        {
            FILE *f = fopen(path.c_str(), "rb");
            if (!f)
                return false;

            std::string all;
            char buf[4096];
            std::size_t got;
            while ((got = fread(buf, 1, sizeof(buf), f)) > 0)
                all.append(buf, got);
            fclose(f);

            std::string line;
            for (std::size_t i = 0; i < all.size(); ++i)
            {
                const char c = all[i];
                if (c == '\n')
                {
                    if (!line.empty() && line[line.size() - 1] == '\r')
                        line.erase(line.size() - 1);
                    out->push_back(line);
                    line.clear();
                }
                else
                {
                    line += c;
                }
            }
            if (!line.empty())
                out->push_back(line);

            return true;
        }
    } // namespace

    ScriptHost::ScriptHost(lancommander::IHttpClient &http,
                           lancommander::GameClient &games,
                           lancommander::ScriptClient &scripts)
        : m_impl(new Impl()),
          m_http(http), m_games(games), m_scripts(scripts),
          m_exec(m_runner),
          m_run_mutex(worker_mutex_create()),
          m_log_mutex(worker_mutex_create()),
          m_busy(false),
          m_debug_enabled(false),
          m_verbose_variables(false),
          m_break_on_entry(false),
          m_next_run_id(1),
          m_line_counter(0),
          m_pump(NULL),
          m_pump_userdata(NULL)
    {
        // A runaway loop in a game's Install.ps1 would otherwise hang the
        // download worker forever with no way to cancel it. Ten million
        // statements is far past anything a real install script does and
        // still ends in a second or two.
        m_runner.set_step_limit(10000000UL);

        m_exec.set_output_callback(
            [this](int stream, const char *bytes, std::size_t len)
            {
                this->feed(m_impl->current_run, stream, bytes, len);
            });
    }

    ScriptHost::~ScriptHost()
    {
        worker_mutex_destroy(m_run_mutex);
        worker_mutex_destroy(m_log_mutex);
        delete m_impl;
    }

    void ScriptHost::lock_log() const { worker_mutex_lock(m_log_mutex); }
    void ScriptHost::unlock_log() const { worker_mutex_unlock(m_log_mutex); }

    bool ScriptHost::busy() const { return m_busy; }

    unsigned long ScriptHost::line_counter() const
    {
        lock_log();
        const unsigned long n = m_line_counter;
        unlock_log();
        return n;
    }

    void ScriptHost::set_credentials(const std::string &base_url,
                                     const std::string &access_token)
    {
        // Applied only with no run in flight, so the client a worker is
        // fetching through is never mutated underneath it. Same discipline as
        // GameArtFetcher::tick.
        if (m_busy)
            return;

        m_http.set_base_url(base_url);
        m_http.set_bearer_token(access_token);
    }

    void ScriptHost::clear_log()
    {
        lock_log();
        m_runs.clear();
        unlock_log();
    }

    // -----------------------------------------------------------------------
    // Log
    // -----------------------------------------------------------------------

    ScriptRunInfo *ScriptHost::find_run(unsigned long run_id)
    {
        for (std::size_t i = 0; i < m_runs.size(); ++i)
        {
            if (m_runs[i].id == run_id)
                return &m_runs[i];
        }
        return NULL;
    }

    void ScriptHost::append(unsigned long run_id, ScriptLineKind kind,
                            const std::string &text)
    {
        lock_log();

        ScriptRunInfo *run = find_run(run_id);
        if (run)
        {
            if (run->lines.size() >= MAX_LINES_PER_RUN)
            {
                // Drop the oldest rather than stopping: a script that fails
                // usually says so at the end, and that is the half worth
                // keeping. The full output is in the log file either way.
                run->lines.erase(run->lines.begin());
                run->lines_trimmed = true;
            }
            run->lines.push_back(ScriptConsoleLine(kind, text));
            m_line_counter++;
        }

        unlock_log();
    }

    void ScriptHost::feed(unsigned long run_id, int stream, const char *bytes,
                          std::size_t len)
    {
        if (run_id == 0)
            return;

        std::string &partial =
            (stream == 0) ? m_impl->partial_out : m_impl->partial_err;
        const ScriptLineKind kind =
            (stream == 0) ? ScriptLineKind::Out : ScriptLineKind::Err;

        for (std::size_t i = 0; i < len; ++i)
        {
            const char c = bytes[i];

            if (c == '\n')
            {
                if (!partial.empty() && partial[partial.size() - 1] == '\r')
                    partial.erase(partial.size() - 1);

                append(run_id, kind, partial);
                log_info("[script] %s", partial.c_str());
                partial.clear();
            }
            else
            {
                partial += c;
            }
        }
    }

    void ScriptHost::flush_partial(unsigned long run_id)
    {
        // A script that ends without a trailing newline still said something.
        if (!m_impl->partial_out.empty())
        {
            append(run_id, ScriptLineKind::Out, m_impl->partial_out);
            log_info("[script] %s", m_impl->partial_out.c_str());
            m_impl->partial_out.clear();
        }
        if (!m_impl->partial_err.empty())
        {
            append(run_id, ScriptLineKind::Err, m_impl->partial_err);
            log_warn("[script] %s", m_impl->partial_err.c_str());
            m_impl->partial_err.clear();
        }
    }

    unsigned long ScriptHost::begin_run(lancommander::ScriptType type,
                                        const ScriptTarget &target)
    {
        lock_log();

        while (m_runs.size() >= MAX_RUNS)
            m_runs.erase(m_runs.begin());

        ScriptRunInfo info;
        info.id = m_next_run_id++;
        info.type = type;
        info.type_name = lancommander::script_type_name(type);
        info.entity_id = target.game_id;
        info.title = target.title;
        info.script_path = lancommander::script::script_file_path(
            target.install_dir, target.game_id, type);
        info.active = true;
        info.started_ms = gfx::ticks_ms();
        info.target = target;

        m_runs.push_back(info);

        const unsigned long id = info.id;
        unlock_log();

        return id;
    }

    void ScriptHost::finish_run(unsigned long run_id,
                                const lancommander::ScriptRun &run, bool ok,
                                const std::string &host_error)
    {
        flush_partial(run_id);

        lock_log();

        ScriptRunInfo *info = find_run(run_id);
        if (info)
        {
            info->active = false;
            info->ran = run.ran;
            info->skipped_runtime = run.skipped_runtime;
            info->success = ok && run.result.success;
            info->exit_code = run.result.exit_code;
            info->return_value = run.result.return_value;
            info->duration_ms = gfx::ticks_ms() - info->started_ms;

            if (!host_error.empty())
                info->error = host_error;
            else if (!run.result.error.empty())
                info->error = run.result.error;
        }

        unlock_log();
    }

    // -----------------------------------------------------------------------
    // Writing scripts to disk
    // -----------------------------------------------------------------------

    bool ScriptHost::write_scripts(const std::string &game_id,
                                   const std::string &title,
                                   const std::string &install_dir)
    {
        // Not a script run, but it shares the console so the user can see why
        // an install ran no scripts. Its own run record would be misleading
        // (there is no exit code), so it logs against the install run that
        // follows -- which does not exist yet. Report through the launcher log
        // and the return value instead, and let the caller narrate.
        auto scripts = m_scripts.get_game_scripts(game_id);

        if (!scripts)
        {
            log_warn("Could not fetch scripts for %s: %s", title.c_str(),
                     scripts.error.c_str());
            return false;
        }

        auto saved = lancommander::script::save_scripts(install_dir, game_id,
                                                        scripts.value);
        if (!saved)
        {
            log_error("Could not write scripts for %s: %s", title.c_str(),
                      saved.error.c_str());
            return false;
        }

        // The manifest is what $GameManifest is injected from, and what the
        // Avalonia launcher reads to know the game is installed. Its absence
        // is not fatal to the scripts, so a failure here is a warning.
        auto manifest_json = m_games.get_manifest_json(game_id);

        if (manifest_json)
        {
            const std::string manifest_path =
                lancommander::manifest::path(install_dir, game_id);
            auto written = lancommander::manifest::write_json(manifest_path,
                                                              manifest_json.value);
            if (!written)
                log_warn("Could not write manifest for %s: %s", title.c_str(),
                         written.error.c_str());
        }
        else
        {
            log_warn("Could not fetch manifest for %s: %s", title.c_str(),
                     manifest_json.error.c_str());
        }

        Impl::Cached cached;
        cached.manifest_json = manifest_json ? manifest_json.value : std::string();
        cached.scripts = scripts.value;

        lock_log();
        m_impl->cache[game_id] = cached;
        unlock_log();

        log_info("Wrote %d script(s) for %s", (int)scripts.value.size(),
                 title.c_str());

        return true;
    }

    // -----------------------------------------------------------------------
    // Redistributables
    // -----------------------------------------------------------------------

    namespace
    {
        void json_append_escaped(std::string &json, const std::string &value)
        {
            for (std::size_t i = 0; i < value.size(); ++i)
            {
                const char c = value[i];
                if (c == '"' || c == '\\')
                    json += '\\';
                json += c;
            }
        }
    } // namespace

    bool ScriptHost::write_redistributable_scripts(const std::string &game_id,
                                                   const std::string &title,
                                                   const std::string &install_dir)
    {
        using namespace lancommander;

        Result<std::vector<Redistributable> > redistributables =
            m_games.get_redistributables(game_id);

        if (!redistributables)
        {
            log_warn("Could not fetch redistributables for %s: %s", title.c_str(),
                     redistributables.error.c_str());
            return false;
        }

        bool all_ok = true;

        for (std::size_t i = 0; i < redistributables.value.size(); ++i)
        {
            const Redistributable &redist = redistributables.value[i];

            if (redist.id.empty())
                continue;

            Result<bool> saved =
                script::save_scripts(install_dir, redist.id, redist.scripts);

            if (!saved)
            {
                log_error("Could not write scripts for redistributable %s: %s",
                          redist.name.c_str(), saved.error.c_str());
                all_ok = false;
                continue;
            }

            // A manifest of its own, so the runtime gating that reads
            // ctx.scripts has something to read and $RedistributableManifest
            // has something to be.
            std::string json = "{\"Id\":\"";
            json_append_escaped(json, redist.id);
            json += "\",\"Name\":\"";
            json_append_escaped(json, redist.name);
            json += "\",\"Scripts\":[";

            for (std::size_t n = 0; n < redist.scripts.size(); ++n)
            {
                char platforms[16];
                sprintf(platforms, "%d", redist.scripts[n].platforms);

                if (n)
                    json += ",";
                json += "{\"Type\":\"";
                json_append_escaped(json, script_type_name(redist.scripts[n].type));
                json += "\",\"Platforms\":";
                json += platforms;
                json += "}";
            }

            json += "]}";

            Result<std::string> written =
                manifest::write_json(manifest::path(install_dir, redist.id), json);

            if (!written)
            {
                log_warn("Could not write manifest for redistributable %s: %s",
                         redist.name.c_str(), written.error.c_str());
            }

            log_info("Wrote %d script(s) for redistributable %s",
                     (int)redist.scripts.size(), redist.name.c_str());
        }

        return all_ok;
    }

    std::vector<std::string> ScriptHost::run_wrapper_redistributables(
        const std::string &install_dir, const std::string &game_id)
    {
        using namespace lancommander;

        std::vector<std::string> ids;

        if (install_dir.empty())
            return ids;

        Result<std::vector<path::DirectoryEntry> > listing =
            path::list_directory(path::combine(install_dir, ".lancommander"));

        if (!listing)
            return ids;

        for (std::size_t i = 0; i < listing.value.size(); ++i)
        {
            if (!listing.value[i].is_directory)
                continue;

            const std::string &id = listing.value[i].name;

            // The game's own metadata directory sits here too. Games have no
            // RunWrapper in the SDK, and skipping it explicitly means a game
            // that somehow grew one cannot end up wrapping its own launch.
            if (id == game_id)
                continue;

            const std::string wrapper =
                script::script_file_path(install_dir, id, ScriptType::RunWrapper);

            if (!wrapper.empty() && path::exists(wrapper))
                ids.push_back(id);
        }

        return ids;
    }

    bool ScriptHost::run_run_wrapper(const ScriptTarget &target,
                                     const std::string &redistributable_id,
                                     const std::string &executable_path,
                                     const std::string &arguments,
                                     const std::string &working_directory,
                                     lancommander::ScriptRun *out)
    {
        using namespace lancommander;

        if (target.install_dir.empty() || redistributable_id.empty())
            return true;

        // Same rule as run(): the drawing thread must never sleep on the run
        // lock, or a script paused on the worker can never be answered.
        if (target.on_ui_thread)
        {
            while (m_busy)
            {
                if (!paused())
                    return false;
                if (!m_pump || !m_pump(m_pump_userdata))
                    return false;
            }
        }

        worker_mutex_lock(m_run_mutex);
        m_busy = true;

        const std::string wrapper_path = script::script_file_path(
            target.install_dir, redistributable_id, ScriptType::RunWrapper);

        const unsigned long run_id = begin_run(ScriptType::RunWrapper, target);

        // The run record's script path is the REDISTRIBUTABLE's, not the
        // game's: that is the file a breakpoint in this run belongs to, and
        // begin_run could only guess at the game's.
        lock_log();
        {
            ScriptRunInfo *info = find_run(run_id);
            if (info)
                info->script_path = wrapper_path;
        }
        unlock_log();

        m_impl->current_run = run_id;
        m_impl->partial_out.clear();
        m_impl->partial_err.clear();
        m_impl->current_script_path = wrapper_path;

        ScriptContext ctx;
        ctx.install_directory = target.install_dir;
        ctx.game_id = target.game_id;
        ctx.redistributable_id = redistributable_id;
        ctx.server_address = target.server_address;
        ctx.default_install_directory = target.default_install_dir;
        ctx.executable_path = executable_path;
        ctx.arguments = arguments;
        ctx.wrapper_working_directory = working_directory;

        {
            Result<GameManifest> game_manifest =
                manifest::read(target.install_dir, target.game_id);
            if (game_manifest)
                ctx.custom_fields = game_manifest.value.custom_fields;

            Result<std::string> game_json = manifest::read_json(
                manifest::path(target.install_dir, target.game_id));
            if (game_json)
                ctx.game_manifest_json = game_json.value;

            // The redistributable's own manifest is what gates this run on the
            // current platform.
            Result<GameManifest> redist_manifest =
                manifest::read(target.install_dir, redistributable_id);
            if (redist_manifest)
                ctx.scripts = redist_manifest.value.scripts;

            Result<std::string> redist_json = manifest::read_json(
                manifest::path(target.install_dir, redistributable_id));
            if (redist_json)
                ctx.redistributable_manifest_json = redist_json.value;
        }

        m_runner.set_debug(m_verbose_variables);
        m_exec.set_debug(m_verbose_variables);
        install_debug_hook(target.on_ui_thread);

        Result<ScriptRun> result = m_exec.redistributable_run_run_wrapper(ctx);

        m_runner.set_debug_hook(nullptr);

        ScriptRun run_info;
        std::string host_error;
        bool ok = false;

        if (result)
        {
            run_info = result.value;
            // A wrapper that is not installed is not a failure -- the caller
            // falls back to starting the game itself.
            ok = run_info.ran ? run_info.result.success : true;
        }
        else
        {
            host_error = result.error;
            append(run_id, ScriptLineKind::Fail, host_error);
        }

        finish_run(run_id, run_info, ok, host_error);

        m_impl->current_run = 0;
        m_busy = false;
        worker_mutex_unlock(m_run_mutex);

        if (out)
            *out = run_info;

        return ok;
    }

    // -----------------------------------------------------------------------
    // Running
    // -----------------------------------------------------------------------

    bool ScriptHost::run(lancommander::ScriptType type,
                         const ScriptTarget &target,
                         lancommander::ScriptRun *out)
    {
        using namespace lancommander;

        if (target.install_dir.empty() || target.game_id.empty())
            return true; // nothing installed; nothing to run

        // The drawing thread must never go to sleep waiting for the
        // interpreter. A script already running on the download worker can
        // stop at a breakpoint, and this thread is the only one that can draw
        // the button that releases it -- so blocking here would deadlock the
        // two against each other: the worker waiting for an answer, the UI
        // waiting for the worker.
        if (target.on_ui_thread)
        {
            while (m_busy)
            {
                if (!paused())
                {
                    // Something is running and not stopped. There is nothing
                    // useful for this thread to do behind it, and the console
                    // offers its button again next frame.
                    return false;
                }

                if (!m_pump || !m_pump(m_pump_userdata))
                    return false;
            }
        }

        // Serialises the interpreter. Held for the whole run, which is why the
        // console reads under a different lock.
        worker_mutex_lock(m_run_mutex);
        m_busy = true;

        const unsigned long run_id = begin_run(type, target);
        m_impl->current_run = run_id;
        m_impl->partial_out.clear();
        m_impl->partial_err.clear();
        m_impl->current_script_path =
            script::script_file_path(target.install_dir, target.game_id, type);

        // What the script needs to know about itself. Read from the cache if
        // write_scripts filled it this session, else from the manifest on
        // disk -- which is what makes BeforeStart work offline, and for a game
        // installed before the launcher was restarted.
        ScriptContext ctx;
        ctx.install_directory = target.install_dir;
        ctx.game_id = target.game_id;
        ctx.server_address = target.server_address;
        ctx.default_install_directory = target.default_install_dir;
        ctx.player_alias = target.player_alias;
        ctx.old_player_alias = target.old_player_alias;
        ctx.new_player_alias = target.new_player_alias;
        ctx.allocated_key = target.allocated_key;

        {
            lock_log();
            std::map<std::string, Impl::Cached>::iterator it =
                m_impl->cache.find(target.game_id);
            const bool hit = (it != m_impl->cache.end());
            if (hit)
            {
                ctx.game_manifest_json = it->second.manifest_json;
                ctx.scripts = it->second.scripts;
            }
            unlock_log();

            if (!hit)
            {
                auto manifest = manifest::read(target.install_dir, target.game_id);
                if (manifest)
                {
                    ctx.scripts = manifest.value.scripts;
                    ctx.custom_fields = manifest.value.custom_fields;
                }

                auto json = manifest::read_json(
                    manifest::path(target.install_dir, target.game_id));
                if (json)
                    ctx.game_manifest_json = json.value;
            }
        }

        m_runner.set_debug(m_verbose_variables);
        m_exec.set_debug(m_verbose_variables);

        install_debug_hook(target.on_ui_thread);

        Result<ScriptRun> result = Result<ScriptRun>::fail("unsupported script type");

        switch (type)
        {
        case ScriptType::Install:     result = m_exec.game_run_install(ctx); break;
        case ScriptType::Uninstall:   result = m_exec.game_run_uninstall(ctx); break;
        case ScriptType::BeforeStart: result = m_exec.game_run_before_start(ctx); break;
        case ScriptType::AfterStop:   result = m_exec.game_run_after_stop(ctx); break;
        case ScriptType::NameChange:  result = m_exec.game_run_name_change(ctx); break;
        case ScriptType::KeyChange:   result = m_exec.game_run_key_change(ctx); break;
        default: break;
        }

        m_runner.set_debug_hook(nullptr);

        ScriptRun run_info;
        std::string host_error;
        bool ok = false;

        if (result)
        {
            run_info = result.value;
            ok = true;

            if (!run_info.ran)
            {
                append(run_id,
                       run_info.skipped_runtime ? ScriptLineKind::Note
                                                : ScriptLineKind::Note,
                       run_info.skipped_runtime
                           ? std::string("skipped: not for this platform")
                           : std::string("no script of this type is installed"));
            }
            else if (!run_info.result.success)
            {
                ok = false;
            }
        }
        else
        {
            host_error = result.error;
            append(run_id, ScriptLineKind::Fail, host_error);
        }

        finish_run(run_id, run_info, ok, host_error);

        {
            // The summary line is what makes the console answer "did it run".
            lock_log();
            const ScriptRunInfo *info = find_run(run_id);
            const bool ran = info && info->ran;
            const bool success = info && info->success;
            const int code = info ? info->exit_code : -1;
            const std::string type_name = info ? info->type_name : std::string();
            const unsigned int ms = info ? info->duration_ms : 0;
            unlock_log();

            if (ran)
            {
                char summary[160];
                sprintf(summary, "%s %s (exit %d, %ums)", type_name.c_str(),
                        success ? "succeeded" : "FAILED", code, ms);
                append(run_id, success ? ScriptLineKind::Note : ScriptLineKind::Fail,
                       summary);
                log_info("Script %s for %s: exit %d", type_name.c_str(),
                         target.title.c_str(), code);
            }
        }

        m_impl->current_run = 0;
        m_busy = false;
        worker_mutex_unlock(m_run_mutex);

        if (out)
            *out = run_info;

        return ok;
    }

    // -----------------------------------------------------------------------
    // Debugger
    // -----------------------------------------------------------------------

    void ScriptHost::set_debug_enabled(bool enabled)
    {
        m_debug_enabled = enabled;

        if (!enabled)
        {
            // Leaving a script paused with the debugger switched off would
            // strand it with no UI to release it.
            resume(ScriptResume::Go);
            lock_log();
            m_impl->step_mode = Impl::StepMode::None;
            unlock_log();
        }
    }

    void ScriptHost::set_verbose_variables(bool enabled)
    {
        m_verbose_variables = enabled;
    }

    void ScriptHost::set_break_on_entry(bool enabled)
    {
        m_break_on_entry = enabled;
    }

    void ScriptHost::set_debug_pump(ScriptDebugPumpFn fn, void *userdata)
    {
        m_pump = fn;
        m_pump_userdata = userdata;
    }

    void ScriptHost::toggle_breakpoint(const std::string &script_path, int line)
    {
        if (line <= 0)
            return;

        lock_log();

        std::vector<int> &lines = m_impl->breakpoints[script_path];
        bool removed = false;

        for (std::size_t i = 0; i < lines.size(); ++i)
        {
            if (lines[i] == line)
            {
                lines.erase(lines.begin() + i);
                removed = true;
                break;
            }
        }

        if (!removed)
            lines.push_back(line);

        unlock_log();
    }

    bool ScriptHost::has_breakpoint(const std::string &script_path, int line) const
    {
        lock_log();

        bool found = false;
        std::map<std::string, std::vector<int> >::const_iterator it =
            m_impl->breakpoints.find(script_path);

        if (it != m_impl->breakpoints.end())
        {
            for (std::size_t i = 0; i < it->second.size(); ++i)
            {
                if (it->second[i] == line)
                {
                    found = true;
                    break;
                }
            }
        }

        unlock_log();

        return found;
    }

    void ScriptHost::clear_breakpoints()
    {
        lock_log();
        m_impl->breakpoints.clear();
        unlock_log();
    }

    int ScriptHost::breakpoint_count() const
    {
        lock_log();

        int n = 0;
        std::map<std::string, std::vector<int> >::const_iterator it;
        for (it = m_impl->breakpoints.begin(); it != m_impl->breakpoints.end(); ++it)
            n += (int)it->second.size();

        unlock_log();

        return n;
    }

    bool ScriptHost::paused() const
    {
        lock_log();
        const bool p = m_impl->paused;
        unlock_log();
        return p;
    }

    ScriptPause ScriptHost::pause_info() const
    {
        lock_log();
        const ScriptPause p = m_impl->pause;
        unlock_log();
        return p;
    }

    void ScriptHost::resume(ScriptResume how)
    {
        lock_log();
        if (m_impl->paused)
            m_impl->resume = how;
        unlock_log();
    }

    void ScriptHost::pause_variables(
        std::vector<std::pair<std::string, std::string> > *out) const
    {
        if (!out)
            return;

        lock_log();
        *out = m_impl->pause_vars;
        unlock_log();
    }

    void ScriptHost::pause_source(std::vector<std::string> *out) const
    {
        if (!out)
            return;

        lock_log();
        *out = m_impl->pause_lines;
        unlock_log();
    }

    void ScriptHost::install_debug_hook(bool on_ui_thread)
    {
        // Nothing to stop for means no hook at all, so an ordinary install
        // runs at full speed even with the console open.
        const bool want =
            m_debug_enabled && (m_break_on_entry || breakpoint_count() > 0);

        if (!want)
        {
            m_runner.set_debug_hook(nullptr);
            return;
        }

        lock_log();
        m_impl->pump_on_this_thread = on_ui_thread;
        m_impl->step_mode = m_break_on_entry ? Impl::StepMode::Into
                                             : Impl::StepMode::None;
        m_impl->step_depth = 0;
        unlock_log();

        m_runner.set_debug_hook(
            [this](const lancommander::ScriptDebugStop &stop,
                   const lancommander::IScriptDebugScope &scope)
            {
                return this->on_debug_stop(stop, scope);
            });
    }

    lancommander::ScriptDebugAction ScriptHost::on_debug_stop(
        const lancommander::ScriptDebugStop &stop,
        const lancommander::IScriptDebugScope &scope)
    {
        using lancommander::ScriptDebugAction;

        // The path, not stop.script_name: every game's install script is
        // called "Install.ps1", so the name identifies nothing.
        const std::string &path = m_impl->current_script_path;

        lock_log();
        const Impl::StepMode mode = m_impl->step_mode;
        const int step_depth = m_impl->step_depth;
        unlock_log();

        bool stop_here = false;

        switch (mode)
        {
        case Impl::StepMode::Into:
            stop_here = true;
            break;
        case Impl::StepMode::Over:
            // Step Over means "finish whatever this line calls". Statements
            // inside a called function are deeper than the one stepped from,
            // so they are run through; the next statement at the original
            // depth (or shallower, if the step returned out of a function) is
            // where control comes back.
            stop_here = (stop.depth <= step_depth);
            break;
        default:
            break;
        }

        if (!stop_here && !has_breakpoint(path, stop.line))
            return ScriptDebugAction::Continue;

        // Snapshot the interpreter while we are allowed to read it. Once this
        // call returns the scope is gone, and the UI thread -- which is where
        // the variables pane is drawn -- could never have read it anyway.
        std::vector<std::pair<std::string, std::string> > vars;
        const std::vector<std::string> names = scope.variable_names();

        for (std::size_t i = 0; i < names.size(); ++i)
        {
            std::string value;
            if (scope.get_variable(names[i], &value))
                vars.push_back(std::make_pair(names[i], value));
        }

        // The source, so the console can show a caret on the paused line.
        // Re-read only when the FILE changed -- keyed on the path, because
        // keyed on the name it would show one game's script against another
        // game's line numbers.
        std::vector<std::string> source;

        lock_log();
        const bool need_source = (m_impl->pause_source_path != path);
        unlock_log();

        if (need_source && !path.empty())
            read_lines(path, &source);

        lock_log();
        m_impl->paused = true;
        m_impl->resume = ScriptResume::None;
        m_impl->pause.script_name = stop.script_name;
        m_impl->pause.script_path = path;
        m_impl->pause.line = stop.line;
        m_impl->pause.col = stop.col;
        m_impl->pause.depth = stop.depth;
        m_impl->pause.stepping = (mode != Impl::StepMode::None);
        m_impl->pause_vars = vars;
        if (need_source)
        {
            m_impl->pause_lines = source;
            m_impl->pause_source_path = path;
        }
        const bool pump_here = m_impl->pump_on_this_thread;
        unlock_log();

        // Wait for the user. Who draws while we do depends on which thread the
        // script is on: a script running on the download worker leaves the
        // main loop free to draw the debugger and answer, while one started
        // from the UI thread has stopped the only thing that draws -- so it
        // has to pump frames itself, which is also the DOS case, where every
        // script runs on that thread.
        ScriptResume answer = ScriptResume::None;

        for (;;)
        {
            lock_log();
            answer = m_impl->resume;
            unlock_log();

            if (answer != ScriptResume::None)
                break;

            if (pump_here)
            {
                if (!m_pump || !m_pump(m_pump_userdata))
                {
                    // No pump installed, or the app is quitting. Either way
                    // nothing is ever going to answer, and blocking forever
                    // would be a hang with no way out.
                    answer = ScriptResume::Abort;
                    break;
                }
            }
            else
            {
                worker_sleep_ms(16);
            }
        }

        lock_log();
        m_impl->paused = false;
        m_impl->resume = ScriptResume::None;

        switch (answer)
        {
        case ScriptResume::StepInto:
            m_impl->step_mode = Impl::StepMode::Into;
            break;
        case ScriptResume::StepOver:
            m_impl->step_mode = Impl::StepMode::Over;
            m_impl->step_depth = stop.depth;
            break;
        default:
            m_impl->step_mode = Impl::StepMode::None;
            break;
        }
        unlock_log();

        if (answer == ScriptResume::Abort)
        {
            append(m_impl->current_run, ScriptLineKind::Fail,
                   "stopped by the debugger");
            return ScriptDebugAction::Abort;
        }

        return ScriptDebugAction::Continue;
    }

    void ScriptHost::breakpoints(std::vector<BreakpointRef> *out) const
    {
        if (!out)
            return;

        out->clear();

        lock_log();

        std::map<std::string, std::vector<int> >::const_iterator it;
        for (it = m_impl->breakpoints.begin(); it != m_impl->breakpoints.end(); ++it)
        {
            for (std::size_t i = 0; i < it->second.size(); ++i)
            {
                BreakpointRef ref;
                ref.script_path = it->first;
                ref.line = it->second[i];

                const std::string::size_type cut = it->first.find_last_of("/\\");
                ref.name = (cut == std::string::npos) ? it->first
                                                      : it->first.substr(cut + 1);

                out->push_back(ref);
            }
        }

        unlock_log();
    }

    bool ScriptHost::run_again(unsigned long run_id, lancommander::ScriptRun *out)
    {
        lancommander::ScriptType type = lancommander::ScriptType::Unknown;
        ScriptTarget target;
        bool found = false;

        lock_log();
        const ScriptRunInfo *info = find_run(run_id);
        if (info)
        {
            type = info->type;
            target = info->target;
            found = true;
        }
        unlock_log();

        if (!found)
            return false;

        // Whatever thread the original ran on, this one is the caller's -- and
        // the only caller is the console, on the thread that draws.
        target.on_ui_thread = true;

        return run(type, target, out);
    }

} // namespace launcher
