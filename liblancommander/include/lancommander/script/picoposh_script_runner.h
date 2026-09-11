#ifndef LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H
#define LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H

#include "script_runner.h"

#include <string>
#include <vector>

namespace lancommander {

// --- debugging ------------------------------------------------------------

// Where the interpreter stopped. Line and column are the script's own, because
// nothing is prepended to the source it runs.
struct ScriptDebugStop {
    std::string script_name;
    int line;
    int col;

    // How many user function calls deep the statement is: 0 at the top level,
    // 1 inside a function the script called. A host implements "step over" by
    // running until the next stop at a depth no greater than the one the step
    // began at; without it, every step dives into every helper.
    int depth;

    ScriptDebugStop() : line(0), col(0), depth(0) {}
};

enum class ScriptDebugAction {
    Continue,  // evaluate the statement and carry on
    Abort      // end the run; it finishes with a runtime error saying so
};

// The variables in scope at a stop, read straight out of the live interpreter.
// Valid only for the duration of the ScriptDebugFn call that was handed it:
// afterwards the interpreter has moved on and the frame may be gone.
//
// Values are the same text form the interpreter would print, which is what a
// debugger pane wants; an object shows as its type, not its fields.
class IScriptDebugScope {
public:
    virtual ~IScriptDebugScope() {}

    // False when the variable is not set at all, which is distinct from being
    // set to $null (where *out is empty).
    virtual bool get_variable(const std::string& name, std::string* out) const = 0;

    // Innermost scope outwards, no duplicates: a name shadowed by a function's
    // local appears once, holding the local's value.
    virtual std::vector<std::string> variable_names() const = 0;
};

// Called before every statement. It may block for as long as it likes -- the
// interpreter has no timers -- which is what makes a breakpoint possible in a
// host with no threads to spare.
//
// It must not start another script; the interpreter is not re-entrant.
typedef std::function<ScriptDebugAction(const ScriptDebugStop&,
                                        const IScriptDebugScope&)> ScriptDebugFn;

// Runs PowerShell-subset scripts through the embedded picoposh interpreter.
//
// The same interpreter runs on Windows 95 through modern Windows, Linux and
// macOS. No external shell, no process spawn.
//
// Variables are handed to the interpreter through picoposh's session API, so
// the script the author wrote is the script that runs: nothing is prepended or
// appended to it, error line numbers are its own, and host values never pass
// through the parser — a value containing quotes, `$(...)` or newlines is just
// data. `$Return` is read back out of the session afterwards, which works even
// when the script ends in `exit`.
//
// IMPORTANT — process-global state. picoposh keeps its output sink and current
// directory in process globals and is documented as single-threaded, so:
//   * only one script may run at a time; a re-entrant call fails immediately
//     rather than corrupting interpreter state;
//   * the runner saves and restores both the sink and the process working
//     directory around each run, because Set-Location inside a script mutates
//     the real CWD and picoposh does not put it back.
class PicoPoshScriptRunner : public IScriptRunner {
public:
    PicoPoshScriptRunner();
    virtual ~PicoPoshScriptRunner();

    ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const ScriptVariableList& variables);

    ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& script_name,
        const std::string& working_directory,
        const ScriptVariableList& variables);

    void set_output_callback(ScriptOutputFn fn);

    // Echoes the injected variable names before the script runs, mirroring the
    // .NET SDK's EnableScriptDebugging. Off by default.
    void set_debug(bool enabled);

    // Upper bound on how much stdout is retained in ScriptResult::output.
    // Output past the cap still reaches the streaming callback. Default 4 MiB;
    // 0 means unlimited.
    void set_output_limit(std::size_t bytes);

    // Abort a script after this many statements, so a runaway loop cannot hang
    // the calling thread. The run ends with a runtime error naming the limit.
    // 0 (the default) means unlimited.
    void set_step_limit(unsigned long steps);

    // Stop before every statement of the script proper. Pass nullptr to
    // detach; with no hook the interpreter runs with none installed, so a
    // caller that is not debugging pays nothing.
    //
    // The hook is installed only around the caller's script, never around the
    // variable-injection preamble or the $Return read-back this class runs in
    // the same session -- breaking inside the runner's own bookkeeping would
    // be confusing and is not something a script author can act on.
    void set_debug_hook(ScriptDebugFn fn);

    // Defined in the .cpp. Only the type name is public, so the C output-sink
    // and debug trampolines can name it; the instance stays private.
    struct Impl;

private:
    Impl* m_impl;

    PicoPoshScriptRunner(const PicoPoshScriptRunner&);
    PicoPoshScriptRunner& operator=(const PicoPoshScriptRunner&);
};

} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H
