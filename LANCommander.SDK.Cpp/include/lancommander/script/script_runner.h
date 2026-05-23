#ifndef LANCOMMANDER_SCRIPT_RUNNER_H
#define LANCOMMANDER_SCRIPT_RUNNER_H

#include <map>
#include <string>

namespace lancommander {

// Result of running a script.
struct ScriptResult {
    bool success;
    int exit_code;
    std::string output;       // Combined stdout (and stderr where captured)
    std::string error;        // Error message if the script could not be launched
};

// Abstract interface for script execution.
//
// The C# SDK uses PowerShell with variable injection. For maximum
// compatibility (Win9x through modern), this interface allows platform-
// specific backends:
//   - BatchScriptRunner  — runs .bat/.cmd files (Windows, including Win9x)
//   - ShellScriptRunner  — runs .sh files via /bin/sh (Unix)
//   - PowerShellRunner   — runs .ps1 files (modern Windows/Linux with pwsh)
//
// Variables are injected as environment variables before execution.
// The C# SDK sets variables like InstallDirectory, GameManifest,
// ServerAddress, PlayerAlias, AllocatedKey, OldPlayerAlias, NewPlayerAlias,
// plus any custom fields from the game manifest.
class IScriptRunner {
public:
    virtual ~IScriptRunner() {}

    // Run a script file with the given variables injected.
    // working_directory: the directory to set as CWD for the script.
    // variables: key-value pairs injected as environment variables.
    virtual ScriptResult run_file(
        const std::string& script_path,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) = 0;

    // Run an inline script string (written to a temp file then executed).
    // working_directory: the directory to set as CWD for the script.
    // variables: key-value pairs injected as environment variables.
    virtual ScriptResult run_inline(
        const std::string& script_contents,
        const std::string& working_directory,
        const std::map<std::string, std::string>& variables) = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_RUNNER_H
