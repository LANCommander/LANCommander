#ifndef LANCOMMANDER_CLIENTS_SCRIPT_EXECUTION_CLIENT_H
#define LANCOMMANDER_CLIENTS_SCRIPT_EXECUTION_CLIENT_H

#include <string>
#include <vector>

#include "../models/custom_field.h"
#include "../models/package.h"
#include "../models/script.h"
#include "../script/script_runner.h"
#include "../types.h"

namespace lancommander {

// Everything a lifecycle script needs. Fields left empty are simply not
// injected, matching how the .NET SDK skips null settings.
struct ScriptContext {
    // --- identity / layout ------------------------------------------------
    std::string install_directory;         // the game's install root
    std::string game_id;
    std::string redistributable_id;        // redistributable_* methods only
    std::string tool_id;                   // tool_* methods only

    // --- settings ---------------------------------------------------------
    std::string default_install_directory; // $DefaultInstallDirectory
    std::string server_address;            // $ServerAddress

    // --- per-lifecycle ----------------------------------------------------
    std::string player_alias;              // $PlayerAlias    (BeforeStart/AfterStop)
    std::string old_player_alias;          // $OldPlayerAlias (NameChange)
    std::string new_player_alias;          // $NewPlayerAlias (NameChange)
    std::string allocated_key;             // $AllocatedKey   (KeyChange)
    std::string executable_path;           // $ExecutablePath (RunWrapper)
    std::string arguments;                 // $Arguments      (RunWrapper)
    std::string wrapper_working_directory; // overrides $WorkingDirectory (RunWrapper)
    std::string latest_archive_path;       // $LatestArchivePath (Package)

    // --- object variables, as raw JSON ------------------------------------
    // Prefer a raw server response body (GameClient::get_manifest_json) over
    // re-serialising a parsed struct: the struct models a handful of fields
    // while real scripts reach into many more.
    std::string game_manifest_json;            // $GameManifest
    std::string redistributable_manifest_json; // $RedistributableManifest
    std::string tool_manifest_json;            // $ToolManifest
    std::string entity_json;                   // $Game / $Redistributable / $Tool

    // --- variable injection + runtime gating ------------------------------
    std::vector<GameCustomField> custom_fields; // one variable each, injected last
    std::vector<Script> scripts;                // metadata for runtime gating
};

struct ScriptRun {
    bool ran;              // false when no script file existed, or it was skipped
    bool skipped_runtime;  // the file existed but this platform is excluded
    int value;             // integer result (see script::result_to_int)
    bool bool_value;       // boolean result (see script::result_to_bool)
    ScriptResult result;   // full stdout/stderr/exit/return payload

    ScriptRun() : ran(false), skipped_runtime(false), value(0), bool_value(false) {}
};

// Runs the game/redistributable/tool lifecycle scripts, porting
// LANCommander.SDK's ScriptClient.{Games,Redistributables,Tools}.
//
// This is deliberately separate from ScriptClient, which fetches scripts over
// HTTP and needs no runner. Splitting them keeps the runner's single-instance
// constraint visible in the type that requires it, and avoids a client whose
// usability depends on which constructor ran. (.NET merges the two only
// because `partial class` makes the file split free.)
//
// Errors follow the SDK convention: a missing or platform-gated script is
// ok({ran = false}), not a failure. A script that runs and exits non-zero is
// also ok — the exit code is the result. Only a runner that could not start,
// or a script that could not be parsed, produces fail().
class ScriptExecutionClient {
public:
    explicit ScriptExecutionClient(IScriptRunner& runner);

    void set_debug(bool enabled);
    void set_output_callback(ScriptOutputFn fn);

    // --- Games ------------------------------------------------------------
    Result<ScriptRun> game_run_install(const ScriptContext& ctx);
    Result<ScriptRun> game_run_uninstall(const ScriptContext& ctx);
    Result<ScriptRun> game_run_before_start(const ScriptContext& ctx);
    Result<ScriptRun> game_run_after_stop(const ScriptContext& ctx);
    Result<ScriptRun> game_run_name_change(const ScriptContext& ctx);
    Result<ScriptRun> game_run_key_change(const ScriptContext& ctx);

    // --- Redistributables -------------------------------------------------
    Result<ScriptRun> redistributable_run_detect_install(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_install(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_uninstall(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_before_start(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_after_stop(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_name_change(const ScriptContext& ctx);
    Result<ScriptRun> redistributable_run_run_wrapper(const ScriptContext& ctx);

    // --- Tools ------------------------------------------------------------
    Result<ScriptRun> tool_run_detect_install(const ScriptContext& ctx);
    Result<ScriptRun> tool_run_install(const ScriptContext& ctx);
    Result<ScriptRun> tool_run_uninstall(const ScriptContext& ctx);
    Result<ScriptRun> tool_run_before_start(const ScriptContext& ctx);
    Result<ScriptRun> tool_run_after_stop(const ScriptContext& ctx);

    // --- Package (inline; the body comes from the server, not from disk) ---
    Result<Package> run_package(const Script& script, const ScriptContext& ctx);

private:
    // Resolve path -> gate on runtime -> run -> map result. Every public
    // method above is a handful of lines over this.
    Result<ScriptRun> run_lifecycle(ScriptType type,
                                    const std::string& entity_id,
                                    const std::string& working_directory,
                                    const ScriptVariableList& variables,
                                    const ScriptContext& ctx);

    static void add_common_variables(ScriptVariableList& variables,
                                     ScriptType type,
                                     const std::string& working_directory,
                                     const ScriptContext& ctx);

    static void add_custom_fields(ScriptVariableList& variables,
                                  const ScriptContext& ctx);

    IScriptRunner& m_runner;
    bool m_debug;
    ScriptOutputFn m_output;
};

// "Install", "NameChange", … — the name injected as $ScriptType.
std::string script_type_name(ScriptType type);

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_SCRIPT_EXECUTION_CLIENT_H
