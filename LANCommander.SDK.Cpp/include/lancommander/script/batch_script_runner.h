#ifndef LANCOMMANDER_SCRIPT_BATCH_RUNNER_H
#define LANCOMMANDER_SCRIPT_BATCH_RUNNER_H

#include "script_runner.h"

namespace lancommander {

// Runs .bat/.cmd scripts via cmd.exe.
//
// Compatible with Windows 95 through modern Windows. Variables are injected
// as environment variables that the batch script can access via %VAR_NAME%.
//
// On Win9x, uses CreateProcessA. On modern Windows, uses CreateProcessA
// with the inherited environment block. The implementation avoids any
// API newer than Win32s.
class BatchScriptRunner : public IScriptRunner {
public:
    ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables);

    ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables);

private:
    // Build a NUL-delimited environment block with the given variables
    // merged on top of the current process environment.
    static std::string build_environment_block(
        const std::map<std::string, std::string>& variables);

    // Execute cmd.exe /C <command> and capture output.
    static ScriptResult execute(
        const std::string& command,
        const std::string& working_directory,
        const std::string& env_block);
};

} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_BATCH_RUNNER_H
