#ifndef LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H
#define LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H

#include "script_runner.h"

namespace lancommander {

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

    // Defined in the .cpp. Only the type name is public, so the C output-sink
    // trampoline can name it; the instance stays private.
    struct Impl;

private:
    Impl* m_impl;

    PicoPoshScriptRunner(const PicoPoshScriptRunner&);
    PicoPoshScriptRunner& operator=(const PicoPoshScriptRunner&);
};

} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_PICOPOSH_RUNNER_H
