#include "lancommander/clients/script_execution_client.h"

#include "lancommander/script/script_helper.h"
#include "lancommander/util/path.h"

#include "picoposh.h"

namespace lancommander {

namespace {

// Redistributable and tool Install/Uninstall/BeforeStart/AfterStop/NameChange
// scripts run from the extracted payload, one level below the metadata dir.
const char* kFilesSubdirectory = "Files";

std::string files_directory(const std::string& install_directory,
                            const std::string& entity_id)
{
    return path::combine(
        script::metadata_directory_path(install_directory, entity_id),
        kFilesSubdirectory);
}

void add_if_set(ScriptVariableList& variables,
                const std::string& name,
                const std::string& value)
{
    if (!value.empty())
        variables.push_back(ScriptVariable::of_string(name, value));
}

void add_json_if_set(ScriptVariableList& variables,
                     const std::string& name,
                     const std::string& json_text)
{
    if (!json_text.empty())
        variables.push_back(ScriptVariable::of_json(name, json_text));
}

} // namespace

std::string script_type_name(ScriptType type)
{
    switch (type) {
        case ScriptType::Install:          return "Install";
        case ScriptType::Uninstall:        return "Uninstall";
        case ScriptType::NameChange:       return "NameChange";
        case ScriptType::KeyChange:        return "KeyChange";
        case ScriptType::SaveUpload:       return "SaveUpload";
        case ScriptType::SaveDownload:     return "SaveDownload";
        case ScriptType::DetectInstall:    return "DetectInstall";
        case ScriptType::BeforeStart:      return "BeforeStart";
        case ScriptType::AfterStop:        return "AfterStop";
        case ScriptType::GameStarted:      return "GameStarted";
        case ScriptType::GameStopped:      return "GameStopped";
        case ScriptType::UserRegistration: return "UserRegistration";
        case ScriptType::UserLogin:        return "UserLogin";
        case ScriptType::ApplicationStart: return "ApplicationStart";
        case ScriptType::Package:          return "Package";
        case ScriptType::RunWrapper:       return "RunWrapper";
        default:                           return "Unknown";
    }
}

ScriptExecutionClient::ScriptExecutionClient(IScriptRunner& runner)
    : m_runner(runner), m_debug(false) {}

void ScriptExecutionClient::set_debug(bool enabled) { m_debug = enabled; }

void ScriptExecutionClient::set_output_callback(ScriptOutputFn fn) { m_output = fn; }

void ScriptExecutionClient::add_common_variables(ScriptVariableList& variables,
                                                 ScriptType type,
                                                 const std::string& working_directory,
                                                 const ScriptContext& ctx)
{
    // $ScriptType is a string here, not the .NET enum object: comparisons like
    // `$ScriptType -eq 'Install'` work, but `$ScriptType.ToString()` does not.
    variables.push_back(ScriptVariable::of_string("ScriptType", script_type_name(type)));
    variables.push_back(ScriptVariable::of_string("WorkingDirectory", working_directory));

    add_if_set(variables, "InstallDirectory", ctx.install_directory);
    add_if_set(variables, "DefaultInstallDirectory", ctx.default_install_directory);
    add_if_set(variables, "ServerAddress", ctx.server_address);
}

void ScriptExecutionClient::add_custom_fields(ScriptVariableList& variables,
                                              const ScriptContext& ctx)
{
    // Appended last on purpose: a custom field named e.g. ServerAddress
    // shadows the well-known variable, exactly as it does in the .NET SDK,
    // where the custom-field loop runs after the fixed AddVariable calls.
    for (std::size_t i = 0; i < ctx.custom_fields.size(); ++i) {
        variables.push_back(ScriptVariable::of_string(ctx.custom_fields[i].name,
                                                      ctx.custom_fields[i].value));
    }
}

Result<ScriptRun> ScriptExecutionClient::run_lifecycle(
    ScriptType type,
    const std::string& entity_id,
    const std::string& working_directory,
    const ScriptVariableList& variables,
    const ScriptContext& ctx)
{
    ScriptRun run;

    const std::string script_path =
        script::script_file_path(ctx.install_directory, entity_id, type);

    if (script_path.empty() || !path::exists(script_path))
        return Result<ScriptRun>::ok(run);

    if (!script::supports_current_runtime(ctx.scripts, type)) {
        run.skipped_runtime = true;
        return Result<ScriptRun>::ok(run);
    }

    m_runner.set_output_callback(m_output);
    run.result = m_runner.run_file(script_path, working_directory, variables);
    run.ran = true;

    // A script that cannot be parsed, or that uses something the interpreter
    // does not implement, is a deployment error rather than a return value.
    // (.NET logs and returns default here; surfacing it is more useful.)
    if (run.result.exit_code == PICO_EXIT_PARSE ||
        run.result.exit_code == PICO_EXIT_UNSUPPORTED) {
        return Result<ScriptRun>::fail(script_path + ": " + run.result.error);
    }

    if (!run.result.success && run.result.exit_code == -1) {
        // The runner never entered the interpreter.
        return Result<ScriptRun>::fail(script_path + ": " + run.result.error);
    }

    script::result_to_int(run.result, &run.value);
    run.bool_value = script::result_to_bool(run.result);

    return Result<ScriptRun>::ok(run);
}

// ---------------------------------------------------------------------------
// Games — working directory is always the install directory
// ---------------------------------------------------------------------------

Result<ScriptRun> ScriptExecutionClient::game_run_install(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Install, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::Install, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::game_run_uninstall(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Uninstall, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::Uninstall, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::game_run_before_start(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::BeforeStart, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_if_set(variables, "PlayerAlias", ctx.player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::BeforeStart, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::game_run_after_stop(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::AfterStop, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_if_set(variables, "PlayerAlias", ctx.player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::AfterStop, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::game_run_name_change(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::NameChange, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_if_set(variables, "OldPlayerAlias", ctx.old_player_alias);
    add_if_set(variables, "NewPlayerAlias", ctx.new_player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::NameChange, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::game_run_key_change(const ScriptContext& ctx)
{
    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::KeyChange, ctx.install_directory, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_if_set(variables, "AllocatedKey", ctx.allocated_key);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::KeyChange, ctx.game_id, ctx.install_directory,
                         variables, ctx);
}

// ---------------------------------------------------------------------------
// Redistributables
// ---------------------------------------------------------------------------

Result<ScriptRun> ScriptExecutionClient::redistributable_run_detect_install(
    const ScriptContext& ctx)
{
    // DetectInstall and RunWrapper run from the metadata directory itself;
    // everything else runs from its Files subdirectory.
    const std::string wd = script::metadata_directory_path(ctx.install_directory,
                                                           ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::DetectInstall, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::DetectInstall, ctx.redistributable_id, wd,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_install(
    const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Install, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::Install, ctx.redistributable_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_uninstall(
    const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Uninstall, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::Uninstall, ctx.redistributable_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_before_start(
    const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::BeforeStart, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_if_set(variables, "PlayerAlias", ctx.player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::BeforeStart, ctx.redistributable_id, wd,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_after_stop(
    const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::AfterStop, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_if_set(variables, "PlayerAlias", ctx.player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::AfterStop, ctx.redistributable_id, wd,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_name_change(
    const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::NameChange, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_if_set(variables, "OldPlayerAlias", ctx.old_player_alias);
    add_if_set(variables, "NewPlayerAlias", ctx.new_player_alias);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::NameChange, ctx.redistributable_id, wd,
                         variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::redistributable_run_run_wrapper(
    const ScriptContext& ctx)
{
    const std::string wd = script::metadata_directory_path(ctx.install_directory,
                                                           ctx.redistributable_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::RunWrapper, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "RedistributableManifest", ctx.redistributable_manifest_json);
    add_if_set(variables, "ExecutablePath", ctx.executable_path);
    add_if_set(variables, "Arguments", ctx.arguments);
    // The list is ordered, so this deliberately overrides the $WorkingDirectory
    // set by add_common_variables — matching the .NET RunWrapper path, where
    // the explicit AddVariable wins.
    add_if_set(variables, "WorkingDirectory", ctx.wrapper_working_directory);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::RunWrapper, ctx.redistributable_id, wd,
                         variables, ctx);
}

// ---------------------------------------------------------------------------
// Tools
//
// Only DetectInstall sees $GameManifest and the game's custom fields; the rest
// get $ToolManifest alone. That asymmetry is inherited from ScriptClient.Tools.
// ---------------------------------------------------------------------------

Result<ScriptRun> ScriptExecutionClient::tool_run_detect_install(const ScriptContext& ctx)
{
    const std::string wd = script::metadata_directory_path(ctx.install_directory,
                                                           ctx.tool_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::DetectInstall, wd, ctx);
    add_json_if_set(variables, "GameManifest", ctx.game_manifest_json);
    add_json_if_set(variables, "ToolManifest", ctx.tool_manifest_json);
    add_custom_fields(variables, ctx);

    return run_lifecycle(ScriptType::DetectInstall, ctx.tool_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::tool_run_install(const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.tool_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Install, wd, ctx);
    add_json_if_set(variables, "ToolManifest", ctx.tool_manifest_json);

    return run_lifecycle(ScriptType::Install, ctx.tool_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::tool_run_uninstall(const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.tool_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Uninstall, wd, ctx);
    add_json_if_set(variables, "ToolManifest", ctx.tool_manifest_json);

    return run_lifecycle(ScriptType::Uninstall, ctx.tool_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::tool_run_before_start(const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.tool_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::BeforeStart, wd, ctx);
    add_json_if_set(variables, "ToolManifest", ctx.tool_manifest_json);

    return run_lifecycle(ScriptType::BeforeStart, ctx.tool_id, wd, variables, ctx);
}

Result<ScriptRun> ScriptExecutionClient::tool_run_after_stop(const ScriptContext& ctx)
{
    const std::string wd = files_directory(ctx.install_directory, ctx.tool_id);

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::AfterStop, wd, ctx);
    add_json_if_set(variables, "ToolManifest", ctx.tool_manifest_json);

    return run_lifecycle(ScriptType::AfterStop, ctx.tool_id, wd, variables, ctx);
}

// ---------------------------------------------------------------------------
// Package — the only lifecycle whose body comes from the server rather than
// from disk, and the only one that runs with no working directory.
// ---------------------------------------------------------------------------

Result<Package> ScriptExecutionClient::run_package(const Script& script,
                                                   const ScriptContext& ctx)
{
    if (script.contents.empty())
        return Result<Package>::fail("package script has no contents");

    if (!script::supports_current_runtime(script.platforms))
        return Result<Package>::fail("package script does not support this platform");

    ScriptVariableList variables;
    add_common_variables(variables, ScriptType::Package, std::string(), ctx);
    add_json_if_set(variables, "Entity", ctx.entity_json);
    add_if_set(variables, "LatestArchivePath", ctx.latest_archive_path);

    m_runner.set_output_callback(m_output);
    const ScriptResult result =
        m_runner.run_inline(script.contents, "Package", std::string(), variables);

    if (!result.error.empty() && !result.has_return_value)
        return Result<Package>::fail("package script failed: " + result.error);

    return script::result_to_package(result);
}

} // namespace lancommander
