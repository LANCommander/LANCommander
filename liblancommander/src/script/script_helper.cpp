#include "lancommander/script/script_helper.h"

#include "lancommander/util/path.h"

#include "json/json_helpers.h"

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#else
#include <unistd.h>
#include <ctime>
#endif

#include <cctype>
#include <cstdio>
#include <cstdlib>

namespace lancommander {
namespace script {

namespace {

const char* kMetadataDirectoryName = ".lancommander";
// Matches ManifestHelper.ManifestFilename in the .NET SDK — both launchers
// read and write the same file. See lancommander/manifest_helper.h.
const char* kManifestFileName = "Manifest.yml";

bool is_truthy(const std::string& text)
{
    return text == "True" || text == "true" || text == "1";
}

} // namespace

std::string script_file_name(ScriptType type)
{
    // A switch rather than a std::map: no allocation, no static-init order to
    // reason about, and it compiles under Open Watcom and VC6.
    //
    // The .NET dictionary throws for the seven types not listed here; we
    // return "" instead, which every caller reads as "not file-backed".
    switch (type) {
        case ScriptType::Install:       return "Install.ps1";
        case ScriptType::Uninstall:     return "Uninstall.ps1";
        case ScriptType::NameChange:    return "ChangeName.ps1";
        case ScriptType::KeyChange:     return "ChangeKey.ps1";
        case ScriptType::DetectInstall: return "DetectInstall.ps1";
        case ScriptType::BeforeStart:   return "BeforeStart.ps1";
        case ScriptType::AfterStop:     return "AfterStop.ps1";
        case ScriptType::Package:       return "Package.ps1";
        case ScriptType::RunWrapper:    return "RunWrapper.ps1";
        default:                        return std::string();
    }
}

ScriptType script_type_from_file_name(const std::string& file_name)
{
    const std::string name = path::base_name(file_name);

    if (name == "Install.ps1")       return ScriptType::Install;
    if (name == "Uninstall.ps1")     return ScriptType::Uninstall;
    if (name == "ChangeName.ps1")    return ScriptType::NameChange;
    if (name == "ChangeKey.ps1")     return ScriptType::KeyChange;
    if (name == "DetectInstall.ps1") return ScriptType::DetectInstall;
    if (name == "BeforeStart.ps1")   return ScriptType::BeforeStart;
    if (name == "AfterStop.ps1")     return ScriptType::AfterStop;
    if (name == "Package.ps1")       return ScriptType::Package;
    if (name == "RunWrapper.ps1")    return ScriptType::RunWrapper;

    return ScriptType::Unknown;
}

std::string metadata_directory_path(const std::string& install_directory,
                                    const std::string& entity_id)
{
    return path::combine(path::combine(install_directory, kMetadataDirectoryName),
                         entity_id);
}

std::string script_file_path(const std::string& install_directory,
                             const std::string& entity_id,
                             ScriptType type)
{
    const std::string file_name = script_file_name(type);
    if (file_name.empty())
        return std::string();

    return path::combine(metadata_directory_path(install_directory, entity_id),
                         file_name);
}

std::string manifest_file_path(const std::string& install_directory,
                               const std::string& entity_id)
{
    return path::combine(metadata_directory_path(install_directory, entity_id),
                         kManifestFileName);
}

std::string script_contents(const Script& script)
{
    if (script.requires_admin)
        return "#Requires -RunAsAdministrator\r\n\r\n" + script.contents;
    return script.contents;
}

Result<bool> save_script(const std::string& install_directory,
                         const std::string& entity_id,
                         const Script& script)
{
    const std::string target =
        script_file_path(install_directory, entity_id, script.type);
    if (target.empty())
        return Result<bool>::ok(false);
    if (script.contents.empty())
        return Result<bool>::ok(false);

    Result<bool> dir = path::create_directories(path::parent(target));
    if (!dir)
        return dir;

    Result<bool> removed = path::remove_file(target);
    if (!removed)
        return removed;

    const std::string body = script_contents(script);

    std::FILE* file = std::fopen(target.c_str(), "wb");
    if (!file)
        return Result<bool>::fail("could not write script: " + target);

    const std::size_t written =
        body.empty() ? 0 : std::fwrite(body.data(), 1, body.size(), file);
    std::fclose(file);

    if (written != body.size())
        return Result<bool>::fail("short write for script: " + target);

    return Result<bool>::ok(true);
}

Result<bool> save_scripts(const std::string& install_directory,
                          const std::string& entity_id,
                          const std::vector<Script>& scripts)
{
    bool wrote_any = false;

    for (std::size_t i = 0; i < scripts.size(); ++i) {
        Result<bool> result = save_script(install_directory, entity_id, scripts[i]);
        if (!result)
            return result;
        if (result.value)
            wrote_any = true;
    }

    return Result<bool>::ok(wrote_any);
}

Result<std::string> save_temp_script(const std::string& contents)
{
    const std::string dir = path::temp_directory();
    static unsigned long counter = 0;

    // GetTempFileNameA is Windows-only and mkstemp is POSIX-only, and neither
    // can produce a .ps1 name in one step — which is why the .NET SDK creates
    // a temp file and then renames it. One portable loop is simpler.
    unsigned long seed;
#ifdef _WIN32
    seed = static_cast<unsigned long>(GetCurrentProcessId()) ^
           static_cast<unsigned long>(GetTickCount());
#else
    seed = static_cast<unsigned long>(::getpid()) ^
           static_cast<unsigned long>(std::time(NULL));
#endif

    for (int attempt = 0; attempt < 64; ++attempt) {
        char name[48];
        std::sprintf(name, "lc_%08lx%04lx.ps1", seed, (counter++ & 0xFFFFUL));

        const std::string full = path::combine(dir, name);

        // Benign TOCTOU: these files are process-local and short-lived, so the
        // worst case is overwriting another attempt's file.
        if (path::exists(full))
            continue;

        std::FILE* file = std::fopen(full.c_str(), "wb");
        if (!file)
            continue;

        std::size_t written = 0;
        if (!contents.empty())
            written = std::fwrite(contents.data(), 1, contents.size(), file);
        std::fclose(file);

        if (written != contents.size()) {
            path::remove_file(full);
            return Result<std::string>::fail("short write for temp script: " + full);
        }

        return Result<std::string>::ok(full);
    }

    return Result<std::string>::fail("could not create a temporary script file in " + dir);
}

Result<std::string> save_temp_script(const Script& script)
{
    return save_temp_script(script_contents(script));
}

RuntimePlatform current_runtime_platform()
{
    // Compile-time detection, which is the right answer for a statically
    // linked native SDK (the .NET SDK asks the runtime instead).
#if defined(_WIN32) || defined(__WIN32__) || defined(__NT__)
    return RuntimePlatform_Windows;
#elif defined(__linux__)
    return RuntimePlatform_Linux;
#elif defined(__APPLE__)
    return RuntimePlatform_macOS;
#else
    return RuntimePlatform_None;
#endif
}

bool supports_current_runtime(int platforms)
{
    // None means "unspecified", which is treated as "runs everywhere" for
    // backwards compatibility with scripts predating the field.
    if (platforms == RuntimePlatform_None)
        return true;

    const RuntimePlatform current = current_runtime_platform();

    // .NET's platforms.HasFlag(None) is always true, so an unrecognised
    // runtime is permissive there. Reproduce that rather than failing closed.
    if (current == RuntimePlatform_None)
        return true;

    return (platforms & static_cast<int>(current)) != 0;
}

bool supports_current_runtime(const std::vector<Script>& scripts, ScriptType type)
{
    for (std::size_t i = 0; i < scripts.size(); ++i) {
        if (scripts[i].type == type)
            return supports_current_runtime(scripts[i].platforms);
    }

    // No metadata for this type — same as RuntimePlatform.None.
    return true;
}

bool result_to_int(const ScriptResult& result, int* out)
{
    if (!out)
        return false;

    if (result.has_return_value && !result.return_value.empty()) {
        const char* text = result.return_value.c_str();
        char* end = NULL;
        const long value = std::strtol(text, &end, 10);
        if (end != text && (*end == '\0' || std::isspace(static_cast<unsigned char>(*end)))) {
            *out = static_cast<int>(value);
            return true;
        }
    }

    // A script may also report an integer by calling `exit <n>`, which aborts
    // evaluation before the return-value epilogue can run.
    *out = result.exit_code;
    return true;
}

bool result_to_bool(const ScriptResult& result)
{
    if (!result.has_return_value)
        return false;

    if (!result.return_json.empty()) {
        if (result.return_json == "true")  return true;
        if (result.return_json == "false") return false;
    }

    return is_truthy(result.return_value);
}

Result<Package> result_to_package(const ScriptResult& result)
{
    if (!result.has_return_value || result.return_json.empty())
        return Result<Package>::fail("script did not set $Return to a package");

    json::JsonDoc doc(result.return_json);
    if (!doc)
        return Result<Package>::fail("script returned a value that is not valid JSON");

    return Result<Package>::ok(json::parse_package(doc.root));
}

} // namespace script
} // namespace lancommander
