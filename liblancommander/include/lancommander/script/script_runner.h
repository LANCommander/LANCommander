#ifndef LANCOMMANDER_SCRIPT_RUNNER_H
#define LANCOMMANDER_SCRIPT_RUNNER_H

#include <cstddef>
#include <functional>
#include <string>
#include <vector>

namespace lancommander {

// How a ScriptVariable's value is rendered into the injected preamble.
enum class ScriptValueKind {
    String,   // a single-quoted PowerShell literal
    Int,      // emitted verbatim as a decimal integer
    Bool,     // $true / $false
    Json,     // ConvertFrom-Json '<value>' — for object variables
    Raw       // emitted verbatim as a PowerShell expression (caller's problem)
};

struct ScriptVariable {
    std::string name;
    ScriptValueKind kind;
    std::string value;

    ScriptVariable() : kind(ScriptValueKind::String) {}

    static ScriptVariable of_string(const std::string& name, const std::string& value);
    static ScriptVariable of_int(const std::string& name, long value);
    static ScriptVariable of_bool(const std::string& name, bool value);
    static ScriptVariable of_json(const std::string& name, const std::string& json_text);
    static ScriptVariable of_raw(const std::string& name, const std::string& expression);
};

// Ordered rather than a map: later entries overwrite earlier ones, which is how
// the .NET SDK layers manifest custom fields on top of the well-known
// variables, and how RunWrapper overrides $WorkingDirectory.
typedef std::vector<ScriptVariable> ScriptVariableList;

struct ScriptResult {
    bool success;              // ran to completion with exit code 0
    int exit_code;             // the script's `exit <n>`, or a PICO_EXIT_* code
    std::string output;        // everything the script wrote to stdout
    std::string error;         // stderr, verbatim — line numbers are the script's own
    bool has_return_value;     // $Return was set to something other than $null
    std::string return_value;  // text form of $Return
    std::string return_json;   // ConvertTo-Json of $Return, for non-scalars

    // The interpreter stopped the script — it did not parse, it used something
    // unimplemented, or it hit a runtime error — as opposed to the script
    // ending on its own terms with `exit <n>`.
    //
    // exit_code cannot answer this. A script that says `exit 2` and a script
    // that does not parse both produce 2, so a host reading only the code
    // reports "your script is broken" for a script that deliberately exited 2.
    // That is the difference between "it ran and told you it failed" and "it
    // never ran", which is the one distinction a script author most needs.
    bool interpreter_error;

    ScriptResult()
        : success(false), exit_code(-1), has_return_value(false),
          interpreter_error(false) {}
};

// stream: 0 = stdout, 1 = stderr. Called synchronously as the script runs,
// with exactly the bytes the script wrote — the runner injects nothing into
// either stream.
typedef std::function<void(int stream, const char* bytes, std::size_t len)> ScriptOutputFn;

// Abstract interface for script execution.
//
// Variables become PowerShell *variables*, not environment variables: the
// embedded interpreter has no $env: provider at all, and the .NET SDK injects
// typed objects ($GameManifest is a manifest, not a string) that an
// environment block could not carry.
class IScriptRunner {
public:
    virtual ~IScriptRunner() {}

    // working_directory: set as the script's location, and as the process CWD
    //   for its duration. Pass "" to leave the current location alone.
    virtual ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const ScriptVariableList& variables) = 0;

    // script_name appears in error messages, so give it something meaningful.
    virtual ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& script_name,
        const std::string& working_directory,
        const ScriptVariableList& variables) = 0;

    // Optional live output tap. Pass nullptr to detach.
    virtual void set_output_callback(ScriptOutputFn fn) = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_RUNNER_H
