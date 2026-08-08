#ifndef LANCOMMANDER_SCRIPT_SCRIPT_HELPER_H
#define LANCOMMANDER_SCRIPT_SCRIPT_HELPER_H

#include <string>
#include <vector>

#include "../models/package.h"
#include "../models/script.h"
#include "../types.h"
#include "script_runner.h"

namespace lancommander {
namespace script {

// On-disk layout and script metadata helpers, mirroring the .NET SDK's
// ScriptHelper so both launchers read and write the same directory structure:
//
//   <install directory>/.lancommander/<entity id>/Install.ps1
//   <install directory>/.lancommander/<entity id>/Manifest.yml

// "Install.ps1", "ChangeName.ps1", … for the nine file-backed script types.
// Returns "" for the seven types that only ever exist inline (SaveUpload,
// SaveDownload, GameStarted, GameStopped, UserRegistration, UserLogin,
// ApplicationStart).
std::string script_file_name(ScriptType type);

// Inverse of the above, for enumerating an existing metadata directory.
// Returns ScriptType::Unknown for anything unrecognised.
ScriptType script_type_from_file_name(const std::string& file_name);

// "<install_directory>/.lancommander/<entity_id>"
std::string metadata_directory_path(const std::string& install_directory,
                                    const std::string& entity_id);

// metadata_directory_path(...) + the script's filename. "" when the type is
// not file-backed.
std::string script_file_path(const std::string& install_directory,
                             const std::string& entity_id,
                             ScriptType type);

// metadata_directory_path(...) + "/Manifest.yml" — the same file the .NET
// SDK's ManifestHelper writes. See lancommander/manifest_helper.h to read it.
std::string manifest_file_path(const std::string& install_directory,
                               const std::string& entity_id);

// The script body as it should be written to disk. Prepends
// "#Requires -RunAsAdministrator" when script.requires_admin, matching the
// .NET SDK byte for byte. picoposh parses that line as an ordinary comment —
// it carries no elevation semantics here.
std::string script_contents(const Script& script);

// Creates missing directories, removes any existing file, writes the contents.
// Returns ok(false) without touching the disk when the script has no contents
// or its type is not file-backed.
Result<bool> save_script(const std::string& install_directory,
                         const std::string& entity_id,
                         const Script& script);

Result<bool> save_scripts(const std::string& install_directory,
                          const std::string& entity_id,
                          const std::vector<Script>& scripts);

// Writes to a fresh "<temp>/lc_XXXXXXXX.ps1". The caller owns deletion.
Result<std::string> save_temp_script(const std::string& contents);
Result<std::string> save_temp_script(const Script& script);

// --- runtime gating ------------------------------------------------------

RuntimePlatform current_runtime_platform();

// Mirrors EnvironmentHelper.SupportsCurrentRuntime, including its permissive
// treatment of None: an unspecified platform set means "runs everywhere".
bool supports_current_runtime(int platforms);

// Looks up `type` in `scripts` and gates on its platforms. Scripts with no
// metadata entry are permitted, matching the .NET behaviour.
bool supports_current_runtime(const std::vector<Script>& scripts, ScriptType type);

// --- typed results -------------------------------------------------------

// $Return when it is numeric, else the script's exit code. Returns false when
// neither is available.
bool result_to_int(const ScriptResult& result, int* out);

// True only when the script actually returned a truthy value. Deliberately
// does NOT fall back to "exit code was 0" — "the script ran cleanly" and
// "the redistributable is installed" are different facts, and conflating them
// would make every well-formed DetectInstall report "installed".
bool result_to_bool(const ScriptResult& result);

Result<Package> result_to_package(const ScriptResult& result);

} // namespace script
} // namespace lancommander

#endif // LANCOMMANDER_SCRIPT_SCRIPT_HELPER_H
