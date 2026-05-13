#ifndef LANCOMMANDER_WIN9X_SCRIPT_RUNNER_H
#define LANCOMMANDER_WIN9X_SCRIPT_RUNNER_H

#include <map>
#include <string>
#include <vector>

#include "script_client.h"

// Saves scripts to disk and invokes them via the system shell. Win9x has no
// PowerShell, so we drop the contents verbatim as .bat files and let
// COMMAND.COM / cmd.exe interpret them. Authors targeting the Win9x launcher
// need to write batch-flavored scripts on the server side.
namespace ScriptRunner
{
    // Saves a single script to <installDir>\.lancommander\<ownerId>\<name>.bat.
    // Creates parent directories. Overwrites any existing file. Returns false
    // only on hard I/O failures.
    bool Save(const std::string& installDir, const std::string& ownerId,
              Script::Type type, const std::string& contents,
              std::string* errorOut);

    // Saves every script in `scripts` whose type is one we wire up. Returns
    // the count actually written. Errors are logged but not fatal — a bad
    // BeforeStart script shouldn't block an install.
    int SaveAll(const std::string& installDir, const std::string& ownerId,
                const std::vector<Script>& scripts);

    bool Exists(const std::string& installDir, const std::string& ownerId,
                Script::Type type);

    // Returns the absolute path the script would live at, or empty for
    // types we don't map. Useful for error messages.
    std::string PathFor(const std::string& installDir,
                        const std::string& ownerId, Script::Type type);

    // Runs the script if it exists. Sets each entry of `vars` as an
    // environment variable (the child process inherits), then ShellExecutes
    // the .bat file and waits for it to exit. Returns the process exit
    // code, or -1 if the script wasn't run at all (didn't exist, or the
    // shell refused).
    int Run(const std::string& installDir, const std::string& ownerId,
            Script::Type type,
            const std::map<std::string, std::string>& vars);
}

#endif
