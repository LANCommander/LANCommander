#include "lancommander/script/picoposh_script_runner.h"

#include "lancommander/util/path.h"

#include "picoposh.h"

#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>

namespace lancommander {

namespace {

// Only one script may run at a time: picoposh keeps its output sink and its
// current directory in process globals and is documented as single-threaded.
bool g_running = false;

struct RunGuard {
    RunGuard() { g_running = true; }
    ~RunGuard() { g_running = false; }
};

const std::size_t kDefaultOutputLimit = 4 * 1024 * 1024;

// The variable the return-value read-back stashes JSON in. Underscored so it
// cannot collide with anything a script would plausibly name.
const char* kReturnJsonVariable = "__LANCommanderReturnJson";

// Frees a picoposh-allocated string on scope exit. picoposh allocates through
// its own allocator, so libc free() would be undefined behaviour.
struct PicoOwnedStr {
    char* p;

    PicoOwnedStr() : p(NULL) {}
    ~PicoOwnedStr() { if (p) pico_string_free(p); }

    std::string str() const { return p ? std::string(p) : std::string(); }

private:
    PicoOwnedStr(const PicoOwnedStr&);
    PicoOwnedStr& operator=(const PicoOwnedStr&);
};

std::string default_name(const std::string& name)
{
    return name.empty() ? std::string("script") : name;
}

bool is_bare_identifier(const std::string& name)
{
    if (name.empty())
        return false;
    if (!(std::isalpha(static_cast<unsigned char>(name[0])) || name[0] == '_'))
        return false;
    for (std::size_t i = 1; i < name.size(); ++i) {
        const unsigned char c = static_cast<unsigned char>(name[i]);
        if (!(std::isalnum(c) || name[i] == '_'))
            return false;
    }
    return true;
}

} // namespace

// ---------------------------------------------------------------------------
// ScriptVariable factories
// ---------------------------------------------------------------------------

ScriptVariable ScriptVariable::of_string(const std::string& name, const std::string& value)
{
    ScriptVariable v;
    v.name = name;
    v.kind = ScriptValueKind::String;
    v.value = value;
    return v;
}

ScriptVariable ScriptVariable::of_int(const std::string& name, long value)
{
    char buffer[32];
    std::sprintf(buffer, "%ld", value);

    ScriptVariable v;
    v.name = name;
    v.kind = ScriptValueKind::Int;
    v.value = buffer;
    return v;
}

ScriptVariable ScriptVariable::of_bool(const std::string& name, bool value)
{
    ScriptVariable v;
    v.name = name;
    v.kind = ScriptValueKind::Bool;
    v.value = value ? "true" : "false";
    return v;
}

ScriptVariable ScriptVariable::of_json(const std::string& name, const std::string& json_text)
{
    ScriptVariable v;
    v.name = name;
    v.kind = ScriptValueKind::Json;
    v.value = json_text;
    return v;
}

ScriptVariable ScriptVariable::of_raw(const std::string& name, const std::string& expression)
{
    ScriptVariable v;
    v.name = name;
    v.kind = ScriptValueKind::Raw;
    v.value = expression;
    return v;
}

// ---------------------------------------------------------------------------
// Impl
// ---------------------------------------------------------------------------

struct PicoPoshScriptRunner::Impl {
    ScriptOutputFn callback;
    bool debug;
    std::size_t output_limit;
    unsigned long step_limit;

    std::string out;
    std::string errors;
    bool truncated;

    Impl()
        : debug(false), output_limit(kDefaultOutputLimit), step_limit(0),
          truncated(false) {}

    void reset()
    {
        out.clear();
        errors.clear();
        truncated = false;
    }

    void on_output(int stream, const char* bytes, std::size_t len)
    {
        if (stream == 0) {
            if (output_limit == 0 || out.size() + len <= output_limit) {
                out.append(bytes, len);
            } else if (out.size() < output_limit) {
                out.append(bytes, output_limit - out.size());
                truncated = true;
            } else {
                truncated = true;
            }
        } else {
            errors.append(bytes, len);
        }

        if (callback)
            callback(stream, bytes, len);
    }
};

extern "C" void lancommander_picoposh_sink(void* userdata, int stream,
                                           const char* bytes, unsigned long len)
{
    PicoPoshScriptRunner::Impl* impl =
        static_cast<PicoPoshScriptRunner::Impl*>(userdata);
    if (impl)
        impl->on_output(stream, bytes, static_cast<std::size_t>(len));
}

// ---------------------------------------------------------------------------
// PicoPoshScriptRunner
// ---------------------------------------------------------------------------

PicoPoshScriptRunner::PicoPoshScriptRunner() : m_impl(new Impl()) {}

PicoPoshScriptRunner::~PicoPoshScriptRunner() { delete m_impl; }

void PicoPoshScriptRunner::set_output_callback(ScriptOutputFn fn)
{
    m_impl->callback = fn;
}

void PicoPoshScriptRunner::set_debug(bool enabled) { m_impl->debug = enabled; }

void PicoPoshScriptRunner::set_output_limit(std::size_t bytes)
{
    m_impl->output_limit = bytes;
}

void PicoPoshScriptRunner::set_step_limit(unsigned long steps)
{
    m_impl->step_limit = steps;
}

ScriptResult PicoPoshScriptRunner::run_inline(
    const std::string& script_contents,
    const std::string& script_name,
    const std::string& working_directory,
    const ScriptVariableList& variables)
{
    ScriptResult result;
    const std::string name = default_name(script_name);

    if (g_running) {
        result.error = "a picoposh script is already running on this process; "
                       "the interpreter is single-threaded and keeps global state";
        return result;
    }

    if (!working_directory.empty() && !path::is_directory(working_directory)) {
        result.error = "working directory does not exist: " + working_directory;
        return result;
    }

    RunGuard guard;

    PicoSession* session = pico_session_new();
    if (!session) {
        result.error = "could not create a picoposh session";
        return result;
    }

    if (m_impl->step_limit != 0)
        pico_session_set_step_limit(session, m_impl->step_limit);

    // --- inject the variables ----------------------------------------------
    //
    // Values go across the API as strings, never through the parser, so no
    // escaping is involved and a name may contain spaces. The kinds that are
    // not strings need a conversion, which runs as its *own* script in the
    // session — session state persists between runs, so the user's script
    // still starts at line 1 and its error line numbers are its own.
    //
    // The working directory needs no setup statement: picoposh's Get-Location
    // reports the process CWD, which we set below before anything runs.
    std::string setup;

    for (std::size_t i = 0; i < variables.size(); ++i) {
        const ScriptVariable& variable = variables[i];

        if (variable.name.empty()) {
            result.error += "skipped a variable with an empty name\n";
            continue;
        }

        if (variable.kind == ScriptValueKind::String) {
            pico_session_set_variable(session, variable.name.c_str(),
                                      variable.value.c_str());
            continue;
        }

        // Everything else needs a conversion statement, which has to name the
        // variable in script text — so it must be a plain identifier.
        if (!is_bare_identifier(variable.name)) {
            result.error += "variable '" + variable.name +
                            "' needs a name usable as $Identifier for its type; "
                            "injected as a string instead\n";
            pico_session_set_variable(session, variable.name.c_str(),
                                      variable.value.c_str());
            continue;
        }

        switch (variable.kind) {
            case ScriptValueKind::Json:
                pico_session_set_variable(session, variable.name.c_str(),
                                          variable.value.c_str());
                setup += "$" + variable.name + " = ConvertFrom-Json $" +
                         variable.name + "\n";
                break;

            case ScriptValueKind::Int:
                pico_session_set_variable(session, variable.name.c_str(),
                                          variable.value.c_str());
                setup += "$" + variable.name + " = [int]$" + variable.name + "\n";
                break;

            case ScriptValueKind::Bool:
                pico_session_set_variable(session, variable.name.c_str(),
                                          variable.value.c_str());
                // Not [bool]: PowerShell casts any non-empty string to $true,
                // so [bool]'false' would be $true. Compare instead.
                setup += "$" + variable.name + " = ($" + variable.name +
                         " -eq 'true')\n";
                break;

            case ScriptValueKind::Raw:
                // A script fragment by definition; it has to be evaluated.
                setup += "$" + variable.name + " = " + variable.value + "\n";
                break;

            default:
                break;
        }
    }

    if (m_impl->debug) {
        setup += "Write-Host '--------- DEBUG ---------'\n";
        setup += "Write-Host \"Working Directory: $(Get-Location)\"\n";
        setup += "Write-Host 'Variables:'\n";
        for (std::size_t i = 0; i < variables.size(); ++i) {
            if (!variables[i].name.empty())
                setup += "Write-Host '    $" + variables[i].name + "'\n";
        }
        setup += "Write-Host '-------------------------'\n";
    }

    m_impl->reset();

    // This both gives the script its working directory (Get-Location reports
    // the process CWD) and has to be undone afterwards, because Set-Location
    // inside a script mutates the real CWD and picoposh never restores it.
    const std::string saved_cwd = path::get_current_directory();
    if (!working_directory.empty())
        path::set_current_directory(working_directory);

    // pico_get_output has no "previous sink" concept beyond this, but saving
    // it means a host that runs scripts from inside its own sink still works.
    void* previous_userdata = NULL;
    pico_output_fn previous_sink = pico_get_output(&previous_userdata);

    pico_set_output(&lancommander_picoposh_sink, m_impl);

    int code = PICO_EXIT_OK;
    bool setup_failed = false;

    if (!setup.empty()) {
        code = pico_session_run(session, setup.c_str(), "<lancommander-setup>");
        if (code != PICO_EXIT_OK)
            setup_failed = true;
    }

    if (!setup_failed)
        code = pico_session_run(session, script_contents.c_str(), name.c_str());

    pico_set_output(previous_sink, previous_userdata);

    if (!saved_cwd.empty())
        path::set_current_directory(saved_cwd);

    // --- read $Return back out ---------------------------------------------
    //
    // This is a separate run, so it works even when the script ended in `exit`
    // — the session outlives it. The JSON goes into a variable rather than to
    // stdout so it cannot pollute the caller's output.
    if (!setup_failed) {
        PicoOwnedStr text;
        if (pico_session_get_variable(session, "Return", &text.p) == 0)
            result.return_value = text.str();

        const std::string json_script =
            "$" + std::string(kReturnJsonVariable) +
            " = ConvertTo-Json -InputObject $Return -Depth 12\n";

        if (pico_session_run(session, json_script.c_str(),
                             "<lancommander-return>") == PICO_EXIT_OK) {
            PicoOwnedStr json;
            if (pico_session_get_variable(session, kReturnJsonVariable, &json.p) == 0)
                result.return_json = json.str();
        }
    }

    pico_session_free(session);

    // "null" is what ConvertTo-Json emits for an unset or explicitly-null
    // $Return, and is how we tell "no value" from a value.
    result.has_return_value =
        !result.return_json.empty() && result.return_json != "null";

    result.exit_code = code;
    result.output = m_impl->out;
    result.error += m_impl->errors;
    result.success = !setup_failed && code == PICO_EXIT_OK;

    if (m_impl->truncated)
        result.output += "\n[output truncated: script exceeded the retained limit]\n";

    if (setup_failed) {
        result.error = "variable injection failed before the script ran: " +
                       result.error;
    }

    return result;
}

ScriptResult PicoPoshScriptRunner::run_file(
    const std::string& script_path,
    const std::string& working_directory,
    const ScriptVariableList& variables)
{
    ScriptResult result;

    // Read the file ourselves rather than calling pico_run_file, which would
    // build its own interpreter and bypass variable injection entirely.
    std::FILE* file = std::fopen(script_path.c_str(), "rb");
    if (!file) {
        result.exit_code = PICO_EXIT_IO;
        result.error = "could not open script: " + script_path;
        return result;
    }

    std::string contents;
    char buffer[4096];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        contents.append(buffer, read);
    std::fclose(file);

    return run_inline(contents, path::base_name(script_path),
                      working_directory, variables);
}

} // namespace lancommander
