#ifndef LAUNCHER_APP_SCRIPT_HOST_H
#define LAUNCHER_APP_SCRIPT_HOST_H

#include <string>
#include <vector>

#include <lancommander/clients/script_execution_client.h>
#include <lancommander/models/script.h>
#include <lancommander/script/picoposh_script_runner.h>

// The launcher's one owner of the embedded picoposh interpreter.
//
// liblancommander has had the whole stack for a while -- PicoPoshScriptRunner,
// ScriptExecutionClient, the cmdlets -- but nothing in the launcher ever
// called it, so a game's Install.ps1 was downloaded, never written to disk and
// never run. This is the seam that changes that, and the reason it exists at
// all is the second half of the job: a script that runs invisibly is a script
// you cannot debug, so every byte it writes is captured here and handed to the
// console screen.
//
// Threading. picoposh keeps its output sink and current directory in process
// globals and is documented as single-threaded, so exactly one script may run
// at a time. Two threads want to start one:
//
//   * the download worker, which runs Install after extraction;
//   * the UI thread, which runs BeforeStart / AfterStop / Uninstall, and
//     whatever the console's Run button asks for.
//
// Two locks rather than one, because they have very different hold times.
// m_run_mutex is held for the whole run and is what serialises the
// interpreter. m_log_mutex is held only across an append or a read of the
// captured output, so the UI can draw the console while a script is still
// running instead of blocking on it for the length of an install.

namespace lancommander
{
    class IHttpClient;
    class GameClient;
    class ScriptClient;
}

namespace launcher
{

    // Where a captured line came from. Out/Err are the script's own bytes;
    // Note/Fail are the host's commentary, and are marked so the console can
    // tell "the script said this" from "the launcher said this about it".
    enum class ScriptLineKind
    {
        Out,
        Err,
        Note,
        Fail
    };

    struct ScriptConsoleLine
    {
        ScriptLineKind kind;
        std::string text;

        ScriptConsoleLine() : kind(ScriptLineKind::Out) {}
        ScriptConsoleLine(ScriptLineKind k, const std::string &t) : kind(k), text(t) {}
    };

    // Everything a lifecycle run needs from the caller. Fields the chosen
    // script type does not use are simply left empty, matching how
    // ScriptExecutionClient skips absent values.
    struct ScriptTarget
    {
        std::string game_id;
        std::string title;
        std::string install_dir;
        std::string server_address;
        std::string default_install_dir;

        std::string player_alias;     // BeforeStart / AfterStop
        std::string old_player_alias; // NameChange
        std::string new_player_alias; // NameChange
        std::string allocated_key;    // KeyChange

        // True when the calling thread is the one that draws. It decides how a
        // breakpoint waits: a script paused on the UI thread has to pump
        // frames itself, because nothing else is going to draw the Continue
        // button it is waiting for. See ScriptHost::set_debug_pump.
        bool on_ui_thread;

        ScriptTarget() : on_ui_thread(true) {}
    };

    // One entry in the console's run list. `id` is stable: the list is bounded
    // and drops its oldest entries, so an index is not something the UI can
    // hold across frames.
    struct ScriptRunInfo
    {
        unsigned long id;
        lancommander::ScriptType type;
        std::string type_name;   // "Install", "BeforeStart", ...
        std::string entity_id;   // the game id
        std::string title;       // the game title, for the list
        std::string script_path; // resolved .ps1, empty when none existed

        bool active;             // still running
        bool ran;                // a script file existed and was executed
        bool skipped_runtime;    // existed, but excluded on this platform
        bool success;
        int exit_code;
        std::string error;        // host-side failure, or the script's stderr
        std::string return_value; // text form of $Return

        unsigned int started_ms;
        unsigned int duration_ms;

        std::vector<ScriptConsoleLine> lines;
        bool lines_trimmed; // output was capped; oldest lines were dropped

        // The inputs this run was given, kept so the console can repeat it
        // verbatim. Re-running with anything less than the original context
        // would be debugging a different script than the one that failed.
        ScriptTarget target;

        ScriptRunInfo()
            : id(0), type(lancommander::ScriptType::Unknown), active(false),
              ran(false), skipped_runtime(false), success(false), exit_code(0),
              started_ms(0), duration_ms(0), lines_trimmed(false) {}
    };

    // --- debugging -----------------------------------------------------------

    // Why the interpreter stopped, and where.
    struct ScriptPause
    {
        // What the interpreter calls the script -- "Install.ps1" -- which is
        // what to show the user.
        std::string script_name;

        // The file it actually came from. Every game's install script is
        // called "Install.ps1", so the short name identifies nothing: keyed on
        // it, a breakpoint set while debugging one game fires in every other
        // game's install, and the source pane shows the wrong file's text
        // against the right file's line numbers.
        std::string script_path;

        int line;
        int col;

        // How many user function calls deep the paused statement is. Step Over
        // runs until the next statement at this depth or shallower.
        int depth;

        bool stepping; // stopped because of a step, not a breakpoint

        ScriptPause() : line(0), col(0), depth(0), stepping(false) {}
    };

    // What the user chose while paused.
    enum class ScriptResume
    {
        None,      // still paused
        StepInto,  // stop again at the very next statement, wherever it is
        StepOver,  // stop at the next statement in this function or its caller
        Go,        // run to the next breakpoint
        Abort      // end the script now
    };

    // Draws one frame while a script is paused on the drawing thread, and
    // returns false when the app wants to quit. Installed by App, which is the
    // only thing that knows how to draw.
    typedef bool (*ScriptDebugPumpFn)(void *userdata);

    class ScriptHost
    {
    public:
        // How much is kept for the console. Output past the per-run cap still
        // reaches the log file; it is the in-memory scrollback that is bounded.
        static const std::size_t MAX_RUNS = 48;
        static const std::size_t MAX_LINES_PER_RUN = 2000;

        // `games` and `scripts` must be backed by `http`, and all three are
        // owned by the caller. They are this object's alone: the install
        // worker fetches through them while the UI thread is using App's own
        // clients, and a shared base URL and bearer token that login rewrites
        // is not something two threads can read at once.
        ScriptHost(lancommander::IHttpClient &http,
                   lancommander::GameClient &games,
                   lancommander::ScriptClient &scripts);
        ~ScriptHost();

        // Applied only when no run is in flight, so the client a worker is
        // using is never mutated underneath it. Called once per frame by App.
        void set_credentials(const std::string &base_url,
                             const std::string &access_token);

        // Fetches the game's scripts and manifest and writes them under
        // <install_dir>/.lancommander/<game_id>/, which is the layout both
        // launchers read. Without this step there is nothing on disk for the
        // lifecycle runs below to execute.
        //
        // A game with no scripts is a success: the directory just gets a
        // manifest. False means the fetch or a write actually failed, and the
        // reason is appended to the console as a Fail line.
        bool write_scripts(const std::string &game_id,
                           const std::string &title,
                           const std::string &install_dir);

        // Fetches the game's redistributables and writes each one's scripts
        // and a small manifest under
        // <install dir>/.lancommander/<redistributable id>/.
        //
        // Without this there is nothing on disk for a RunWrapper to be, and
        // the launcher would go on starting games directly for redistributables
        // that exist precisely to wrap that launch.
        bool write_redistributable_scripts(const std::string &game_id,
                                           const std::string &title,
                                           const std::string &install_dir);

        // The redistributable ids installed alongside `game_id` that have a
        // RunWrapper script. Reads the disk, so it works offline.
        static std::vector<std::string> run_wrapper_redistributables(
            const std::string &install_dir, const std::string &game_id);

        // Runs a redistributable's RunWrapper. The script starts the game
        // itself and does not return until it has exited, so this BLOCKS for
        // the length of the play session -- which is why the caller treats it
        // the way DOS already treats every launch.
        bool run_run_wrapper(const ScriptTarget &target,
                             const std::string &redistributable_id,
                             const std::string &executable_path,
                             const std::string &arguments,
                             const std::string &working_directory,
                             lancommander::ScriptRun *out);

        // Runs one lifecycle script. Returns false only when the run itself
        // could not be attempted or the script exited non-zero; a script that
        // does not exist, or is gated out on this platform, is true with
        // `out->ran` false -- the same convention ScriptExecutionClient uses.
        //
        // `out` may be NULL.
        bool run(lancommander::ScriptType type, const ScriptTarget &target,
                 lancommander::ScriptRun *out);

        // True while a script is executing, on any thread.
        bool busy() const;

        // Repeats a recorded run with the inputs it originally had, re-reading
        // the .ps1 from disk. This is what makes the debugger a loop rather
        // than a one-shot: edit the script, run it again, without reinstalling
        // the game to get the install script to fire.
        //
        // Runs on the CALLING thread, so the console must not offer it while
        // something else is running -- see busy().
        bool run_again(unsigned long run_id, lancommander::ScriptRun *out);

        // --- console access ---------------------------------------------------
        //
        // The returned reference is only valid between lock_log() and
        // unlock_log(). Holding it across the draw of one panel is the intended
        // use; holding it across a frame is not.
        void lock_log() const;
        void unlock_log() const;
        const std::vector<ScriptRunInfo> &runs() const { return m_runs; }

        // Total lines appended since startup, for the console's "new output"
        // check -- cheaper than diffing the run list, and it survives trimming.
        unsigned long line_counter() const;

        void clear_log();

        // --- debugging --------------------------------------------------------

        // Off by default. With it off the interpreter runs with no per-statement
        // hook at all, so a launcher nobody is debugging pays nothing for this.
        void set_debug_enabled(bool enabled);
        bool debug_enabled() const { return m_debug_enabled; }

        // Echoes the injected variable names before each script runs, mirroring
        // the .NET SDK's EnableScriptDebugging.
        void set_verbose_variables(bool enabled);
        bool verbose_variables() const { return m_verbose_variables; }

        // Breakpoints are keyed on the script's full PATH, not its file name.
        // Every game's install script is called "Install.ps1", so a file name
        // identifies nothing -- keyed on it, a breakpoint armed while debugging
        // one game fires in every other game's install too.
        void toggle_breakpoint(const std::string &script_path, int line);
        bool has_breakpoint(const std::string &script_path, int line) const;
        void clear_breakpoints();
        int breakpoint_count() const;

        // Every armed breakpoint, for a list the user can see and prune. The
        // path is the key; `name` is the short form to show.
        struct BreakpointRef
        {
            std::string script_path;
            std::string name;
            int line;

            BreakpointRef() : line(0) {}
        };
        void breakpoints(std::vector<BreakpointRef> *out) const;

        // Stop on the very first statement of the next script that runs. The
        // way to debug a script the launcher starts on its own, where there is
        // no earlier moment to set a breakpoint from.
        void set_break_on_entry(bool enabled);
        bool break_on_entry() const { return m_break_on_entry; }

        void set_debug_pump(ScriptDebugPumpFn fn, void *userdata);

        // --- paused state, read by the debugger UI ----------------------------

        bool paused() const;
        ScriptPause pause_info() const;

        // Answers the pause. Ignored when nothing is paused.
        void resume(ScriptResume how);

        // The variables in scope at the pause, as "name" / text pairs. Empty
        // when not paused. Reading these runs no script: the values come out of
        // the live interpreter through picoposh's debug accessors.
        void pause_variables(std::vector<std::pair<std::string, std::string> > *out) const;

        // Source of the script that is paused (or was, most recently), split
        // into lines. Kept here rather than re-read by the UI so the console
        // and the overlay agree about which line 12 is.
        void pause_source(std::vector<std::string> *out) const;

    private:
        struct Impl;
        Impl *m_impl;

        lancommander::IHttpClient &m_http;
        lancommander::GameClient &m_games;
        lancommander::ScriptClient &m_scripts;

        lancommander::PicoPoshScriptRunner m_runner;
        lancommander::ScriptExecutionClient m_exec;

        std::vector<ScriptRunInfo> m_runs;

        void *m_run_mutex;
        void *m_log_mutex;

        volatile bool m_busy;
        bool m_debug_enabled;
        bool m_verbose_variables;
        bool m_break_on_entry;

        unsigned long m_next_run_id;
        unsigned long m_line_counter;

        ScriptDebugPumpFn m_pump;
        void *m_pump_userdata;

        // Begins a run record and returns its id. Log lock taken internally.
        unsigned long begin_run(lancommander::ScriptType type,
                                const ScriptTarget &target);
        void finish_run(unsigned long run_id, const lancommander::ScriptRun &run,
                        bool ok, const std::string &host_error);

        void append(unsigned long run_id, ScriptLineKind kind,
                    const std::string &text);

        // Splits picoposh's arbitrary byte chunks into lines. The tail of a
        // chunk that does not end in a newline is held until the next one, so
        // Write-Host output is not shredded across console entries.
        void feed(unsigned long run_id, int stream, const char *bytes,
                  std::size_t len);
        void flush_partial(unsigned long run_id);

        ScriptRunInfo *find_run(unsigned long run_id);

        // Attaches the per-statement hook for one run, or detaches it when
        // debugging is off or there is nothing to stop for. `on_ui_thread`
        // decides how a stop waits -- see ScriptTarget::on_ui_thread.
        void install_debug_hook(bool on_ui_thread);

        // Called by the interpreter before every statement, on whichever
        // thread the script is running on.
        lancommander::ScriptDebugAction on_debug_stop(
            const lancommander::ScriptDebugStop &stop,
            const lancommander::IScriptDebugScope &scope);

        ScriptHost(const ScriptHost &);
        ScriptHost &operator=(const ScriptHost &);
    };

} // namespace launcher

#endif // LAUNCHER_APP_SCRIPT_HOST_H
