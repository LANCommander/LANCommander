#include "test_main.h"

#include "lancommander/script/script_helper.h"
#include "lancommander/util/path.h"

#include <cstdio>

using namespace lancommander;

namespace {

std::string read_file(const std::string& path)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return std::string();

    std::string out;
    char buffer[1024];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out.append(buffer, read);
    std::fclose(file);

    return out;
}

// Joins with the platform separator. Note that combine() only ever *appends*
// a separator — separators already present in an argument are left alone, so
// "C:/Games" stays "C:/Games" even on Windows.
std::string join(const std::string& a, const std::string& b)
{
    return a + path::separator() + b;
}

} // namespace

void test_script_helper()
{
    // --- the nine file-backed types, and the seven that are not ------------
    CHECK_EQ(script::script_file_name(ScriptType::Install), "Install.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::Uninstall), "Uninstall.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::NameChange), "ChangeName.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::KeyChange), "ChangeKey.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::DetectInstall), "DetectInstall.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::BeforeStart), "BeforeStart.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::AfterStop), "AfterStop.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::Package), "Package.ps1");
    CHECK_EQ(script::script_file_name(ScriptType::RunWrapper), "RunWrapper.ps1");

    CHECK_EQ(script::script_file_name(ScriptType::SaveUpload), "");
    CHECK_EQ(script::script_file_name(ScriptType::SaveDownload), "");
    CHECK_EQ(script::script_file_name(ScriptType::GameStarted), "");
    CHECK_EQ(script::script_file_name(ScriptType::GameStopped), "");
    CHECK_EQ(script::script_file_name(ScriptType::UserRegistration), "");
    CHECK_EQ(script::script_file_name(ScriptType::UserLogin), "");
    CHECK_EQ(script::script_file_name(ScriptType::ApplicationStart), "");
    CHECK_EQ(script::script_file_name(ScriptType::Unknown), "");

    CHECK(script::script_type_from_file_name("Install.ps1") == ScriptType::Install);
    CHECK(script::script_type_from_file_name("ChangeName.ps1") == ScriptType::NameChange);
    CHECK(script::script_type_from_file_name("nope.ps1") == ScriptType::Unknown);

    // --- the on-disk layout shared with the .NET SDK ------------------------
    const std::string metadata = join(join("C:/Games/Q3", ".lancommander"), "abc-123");

    CHECK_EQ(script::metadata_directory_path("C:/Games/Q3", "abc-123"), metadata);
    CHECK_EQ(script::script_file_path("C:/Games/Q3", "abc-123", ScriptType::Install),
             join(metadata, "Install.ps1"));
    CHECK_EQ(script::script_file_path("C:/Games/Q3", "abc-123", ScriptType::GameStarted), "");
    // Manifest.yml, not .json — the same file the .NET SDK's ManifestHelper
    // writes, so an install directory is shared between both launchers.
    CHECK_EQ(script::manifest_file_path("C:/Games/Q3", "abc-123"),
             join(metadata, "Manifest.yml"));

    // --- the admin directive is preserved for .NET interop -----------------
    {
        Script s;
        s.contents = "Write-Host 'hi'";
        CHECK_EQ(script::script_contents(s), "Write-Host 'hi'");

        s.requires_admin = true;
        CHECK_EQ(script::script_contents(s),
                 "#Requires -RunAsAdministrator\r\n\r\nWrite-Host 'hi'");
    }

    // --- runtime gating -----------------------------------------------------
    {
        const RuntimePlatform current = script::current_runtime_platform();

        // Unspecified means "runs everywhere".
        CHECK(script::supports_current_runtime(RuntimePlatform_None));
        CHECK(script::supports_current_runtime(static_cast<int>(current)));
        CHECK(script::supports_current_runtime(
            static_cast<int>(current) | RuntimePlatform_Linux |
            RuntimePlatform_Windows | RuntimePlatform_macOS));

        const int foreign = (current == RuntimePlatform_Windows)
                                ? RuntimePlatform_Linux
                                : RuntimePlatform_Windows;
        CHECK(!script::supports_current_runtime(foreign));

        std::vector<Script> scripts;
        Script gated;
        gated.type = ScriptType::Install;
        gated.platforms = foreign;
        scripts.push_back(gated);

        CHECK(!script::supports_current_runtime(scripts, ScriptType::Install));
        // A type with no metadata entry is permitted, matching .NET.
        CHECK(script::supports_current_runtime(scripts, ScriptType::Uninstall));
    }

    // --- temp scripts -------------------------------------------------------
    {
        Result<std::string> first = script::save_temp_script(std::string("one\n"));
        Result<std::string> second = script::save_temp_script(std::string("two\n"));

        CHECK(first.success);
        CHECK(second.success);
        CHECK(first.value != second.value);
        CHECK(first.value.size() > 4 &&
              first.value.compare(first.value.size() - 4, 4, ".ps1") == 0);
        CHECK_EQ(read_file(first.value), "one\n");

        path::remove_file(first.value);
        path::remove_file(second.value);
    }

    // --- save_script creates directories and overwrites ---------------------
    {
        const std::string root =
            path::combine(path::temp_directory(), "lc_script_helper_test");
        const std::string entity = "11111111-2222-3333-4444-555555555555";

        Script s;
        s.type = ScriptType::Install;
        s.contents = "Write-Host 'first'";

        Result<bool> saved = script::save_script(root, entity, s);
        CHECK(saved.success);
        CHECK(saved.value);

        const std::string target =
            script::script_file_path(root, entity, ScriptType::Install);
        CHECK(path::exists(target));
        CHECK_EQ(read_file(target), "Write-Host 'first'");

        s.contents = "Write-Host 'second'";
        saved = script::save_script(root, entity, s);
        CHECK(saved.success);
        CHECK_EQ(read_file(target), "Write-Host 'second'");

        // A type with no file, and a script with no body, are both no-ops.
        Script inline_only;
        inline_only.type = ScriptType::GameStarted;
        inline_only.contents = "Write-Host 'never written'";
        saved = script::save_script(root, entity, inline_only);
        CHECK(saved.success);
        CHECK(!saved.value);

        Script empty;
        empty.type = ScriptType::Uninstall;
        saved = script::save_script(root, entity, empty);
        CHECK(saved.success);
        CHECK(!saved.value);
        CHECK(!path::exists(script::script_file_path(root, entity, ScriptType::Uninstall)));

        path::remove_file(target);
    }

    // --- path helpers -------------------------------------------------------
    CHECK_EQ(path::combine("a", "b"), join("a", "b"));
    // An existing separator is reused rather than duplicated or rewritten.
    CHECK_EQ(path::combine("a/", "b"), "a/b");
    CHECK_EQ(path::combine("", "b"), "b");
    CHECK_EQ(path::combine("a", ""), "a");
    CHECK_EQ(path::base_name(join(join("a", "b"), "c.ps1")), "c.ps1");
    CHECK_EQ(path::parent(join(join("a", "b"), "c.ps1")), join("a", "b"));
    CHECK(path::is_directory(path::temp_directory()));
    CHECK(!path::exists(path::combine(path::temp_directory(), "lc_nope_4b2c9")));
}
