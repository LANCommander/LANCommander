#include "test_main.h"

#include "lancommander/manifest_helper.h"
#include "lancommander/script/cmdlets.h"
#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>

using namespace lancommander;

namespace {

std::string scratch_dir()
{
    const std::string dir = path::combine(path::temp_directory(), "lc_cmdlet_test");
    path::create_directories(dir);
    return dir;
}

bool write_file(const std::string& path, const std::string& contents)
{
    path::create_directories(path::parent(path));

    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;
    if (!contents.empty())
        std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);
    return true;
}

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

// Runs a script and returns $Return's text form.
std::string run(const std::string& source,
                const std::string& working_directory = std::string())
{
    PicoPoshScriptRunner runner;
    const ScriptResult r =
        runner.run_inline(source, "cmdlet-test", working_directory,
                          ScriptVariableList());

    if (!r.error.empty())
        std::printf("  (script error: %s)\n", r.error.c_str());

    return r.return_value;
}

ScriptResult run_full(const std::string& source,
                      const std::string& working_directory = std::string())
{
    PicoPoshScriptRunner runner;
    return runner.run_inline(source, "cmdlet-test", working_directory,
                             ScriptVariableList());
}

} // namespace

void test_cmdlets()
{
    Result<bool> registered = cmdlets::register_all();
    CHECK(registered.success);
    CHECK(cmdlets::names().size() == 13);

    // Every registered name must actually resolve in the interpreter.
    {
        const std::vector<std::string> names = cmdlets::names();
        for (std::size_t i = 0; i < names.size(); ++i) {
            const ScriptResult r = run_full(
                "$Return = (Get-Command '" + names[i] + "') -ne $null\n");
            CHECK(r.error.empty());
        }
    }

    // --- Get-Runtime --------------------------------------------------------
    {
        const std::string platform = run("$Return = Get-Runtime\n");
#ifdef _WIN32
        CHECK_EQ(platform, "Windows");
#else
        CHECK(platform == "Linux" || platform == "macOS");
#endif
    }

    // --- Get-SanitizedPath --------------------------------------------------
    {
        // The colon-in-title rule, which is why it is not a plain strip.
        CHECK_EQ(run("$Return = Get-SanitizedPath 'Half-Life: Opposing Force'\n"),
                 "Half-Life - Opposing Force");
        // A colon not between word characters is simply removed.
        CHECK_EQ(run("$Return = Get-SanitizedPath 'C:/Games'\n"), "CGames");
        CHECK_EQ(run("$Return = Get-SanitizedPath 'a<b>c|d?e*f'\n"), "abcdef");
        CHECK_EQ(run("$Return = Get-SanitizedPath 'Trailing.'\n"), "Trailing");
        CHECK_EQ(run("$Return = Get-SanitizedPath 'Clean Name'\n"), "Clean Name");
    }

    // --- Convert-AspectRatio ------------------------------------------------
    {
        // The example from LANCommander's own cmdlet documentation:
        //   Convert-AspectRatio -Width 2560 -Height 1440 -AspectRatio (4 / 3)
        //   -> Width 1920, Height 1440
        CHECK_EQ(run("$r = Convert-AspectRatio -Width 2560 -Height 1440 "
                     "-AspectRatio (4 / 3)\n$Return = $r.Width\n"),
                 "1920");
        CHECK_EQ(run("$r = Convert-AspectRatio -Width 2560 -Height 1440 "
                     "-AspectRatio (4 / 3)\n$Return = $r.Height\n"),
                 "1440");
    }

    // --- the FOV pair -------------------------------------------------------
    {
        // 4:3 in, 4:3 base out: the FOV is unchanged.
        CHECK_EQ(run("$Return = Get-HorizontalFov -Width 1024 -Height 768 -BaseFov 90\n"),
                 "90");
        // 16:9 widens a 90-degree 4:3 horizontal FOV to 106.
        CHECK_EQ(run("$Return = Get-HorizontalFov -Width 1920 -Height 1080 -BaseFov 90\n"),
                 "106");
        CHECK_EQ(run("$Return = Get-VerticalFov -Width 1024 -Height 768 -BaseFov 75\n"),
                 "75");
        // 16:9 narrows the vertical FOV.
        CHECK_EQ(run("$Return = Get-VerticalFov -Width 1920 -Height 1080 -BaseFov 75\n"),
                 "60");
    }

    // --- Get-PrimaryDisplay -------------------------------------------------
    {
        const ScriptResult r = run_full(
            "$d = Get-PrimaryDisplay\n$Return = $d.Width\n");
        CHECK(r.success);
#ifdef _WIN32
        // A real display should report a positive width.
        CHECK(r.return_value != "0");
#endif
    }

    // --- ConvertTo-StringBytes ----------------------------------------------
    //
    // Checked against the examples in LANCommander.Documentation/Scripting/
    // Cmdlets.md so the two implementations agree byte for byte.
    {
        CHECK_EQ(run("$Return = (ConvertTo-StringBytes -Input 'Hi') -join ','\n"),
                 "72,105");

        CHECK_EQ(run("$Return = (ConvertTo-StringBytes -Input 'Hello' -Utf16 1 "
                     "-BigEndian 1) -join ','\n"),
                 "0,72,0,101,0,108,0,108,0,111");

        CHECK_EQ(run("$Return = (ConvertTo-StringBytes -Input 'Hello' -Utf16 1) "
                     "-join ','\n"),
                 "72,0,101,0,108,0,108,0,111,0");

        CHECK_EQ(run("$Return = (ConvertTo-StringBytes -Input 'Hello' -MaxLength 10 "
                     "-MinLength 10) -join ','\n"),
                 "72,101,108,108,111,0,0,0,0,0");

        CHECK_EQ(run("$Return = (ConvertTo-StringBytes -Input 'Hello, world!' "
                     "-MaxLength 5) -join ','\n"),
                 "72,101,108,108,111");
    }

    // --- Edit-PatchBinary ---------------------------------------------------
    {
        const std::string target = path::combine(scratch_dir(), "patch.bin");
        CHECK(write_file(target, "AAAAAAAAAA"));

        const ScriptResult r = run_full(
            "$bytes = ConvertTo-StringBytes -Input 'ZZ'\n"
            "Edit-PatchBinary -Offset 3 -Data $bytes -FilePath '" + target + "'\n"
            "$Return = 'done'\n");

        CHECK(r.success);
        // Patched in place: the surrounding bytes must survive, and the file
        // must not have been truncated.
        CHECK_EQ(read_file(target), "AAAZZAAAAA");

        path::remove_file(target);
    }

    // --- Write-ReplaceContentInFile ----------------------------------------
    {
        const std::string target = path::combine(scratch_dir(), "config.txt");
        CHECK(write_file(target, "name=old\nother=keep\n"));

        const ScriptResult r = run_full(
            "$Return = Write-ReplaceContentInFile 'name=old' 'name=new' '" +
            target + "'\n");

        CHECK(r.success);
        CHECK_EQ(read_file(target), "name=new\nother=keep\n");

        // Capture-group substitution.
        CHECK(write_file(target, "port 27960\n"));
        run_full("Write-ReplaceContentInFile 'port ([0-9]*)' 'PORT=$1' '" +
                 target + "'\n");
        CHECK_EQ(read_file(target), "PORT=27960\n");

        path::remove_file(target);
    }

    // --- Update-IniValue ----------------------------------------------------
    {
        const std::string target = path::combine(scratch_dir(), "game.ini");

        // Updating an existing key leaves comments, spacing and other
        // sections byte-identical.
        CHECK(write_file(target,
                         "; a comment\n"
                         "[Display]\n"
                         "Width=640\n"
                         "Height=480\n"
                         "\n"
                         "[Audio]\n"
                         "Volume=100\n"));

        run_full("Update-IniValue -Section 'Display' -Key 'Width' -Value '1920' "
                 "-FilePath '" + target + "'\n");

        CHECK_EQ(read_file(target),
                 "; a comment\n"
                 "[Display]\n"
                 "Width=1920\n"
                 "Height=480\n"
                 "\n"
                 "[Audio]\n"
                 "Volume=100\n");

        // A missing key is added inside its section, not at the end of file.
        run_full("Update-IniValue -Section 'Display' -Key 'Fullscreen' -Value '1' "
                 "-FilePath '" + target + "'\n");

        CHECK_EQ(read_file(target),
                 "; a comment\n"
                 "[Display]\n"
                 "Width=1920\n"
                 "Height=480\n"
                 "Fullscreen=1\n"
                 "\n"
                 "[Audio]\n"
                 "Volume=100\n");

        // A missing section is appended.
        run_full("Update-IniValue -Section 'Network' -Key 'Port' -Value '27960' "
                 "-FilePath '" + target + "'\n");
        CHECK(read_file(target).find("[Network]\nPort=27960\n") != std::string::npos);

        // -OnlyRemove deletes the key and writes nothing back.
        run_full("Update-IniValue -Section 'Audio' -Key 'Volume' -Value '' "
                 "-OnlyRemove -FilePath '" + target + "'\n");
        CHECK(read_file(target).find("Volume=") == std::string::npos);
        CHECK(read_file(target).find("[Audio]") != std::string::npos);

        // -NoAdd refuses to create a key that is not already there.
        run_full("Update-IniValue -Section 'Audio' -Key 'Muted' -Value '1' "
                 "-NoAdd -FilePath '" + target + "'\n");
        CHECK(read_file(target).find("Muted") == std::string::npos);

        // Quote wrapping: absent follows the existing value, true forces.
        CHECK(write_file(target, "[Player]\nName=\"Old\"\nRank=1\n"));

        run_full("Update-IniValue -Section 'Player' -Key 'Name' -Value 'New' "
                 "-FilePath '" + target + "'\n");
        CHECK(read_file(target).find("Name=\"New\"") != std::string::npos);

        run_full("Update-IniValue -Section 'Player' -Key 'Rank' -Value '2' "
                 "-WrapValueInQuotes 1 -FilePath '" + target + "'\n");
        CHECK(read_file(target).find("Rank=\"2\"") != std::string::npos);

        run_full("Update-IniValue -Section 'Player' -Key 'Rank' -Value '\"3\"' "
                 "-WrapValueInQuotes 0 -FilePath '" + target + "'\n");
        CHECK(read_file(target).find("Rank=3") != std::string::npos);

        // A missing file is a silent no-op, as in .NET.
        const ScriptResult missing = run_full(
            "Update-IniValue -Section 'a' -Key 'b' -Value 'c' "
            "-FilePath 'no_such_file_9f3a.ini'\n$Return = 'survived'\n");
        CHECK(missing.success);
        CHECK_EQ(missing.return_value, "survived");

        path::remove_file(target);
    }

    // --- New-Package --------------------------------------------------------
    {
        const ScriptResult r = run_full(
            "New-Package -Path 'out.zip' -Version '1.2' -Changelog 'notes' "
            "| Out-Null\n");

        CHECK(r.success);
        // The cmdlet assigns $Return itself, so a Package script needs no
        // further step.
        CHECK(r.has_return_value);
        CHECK(r.return_json.find("\"Path\":\"out.zip\"") != std::string::npos);
        CHECK(r.return_json.find("\"Version\":\"1.2\"") != std::string::npos);
    }

    // --- the manifest pair --------------------------------------------------
    {
        const std::string root = path::combine(scratch_dir(), "install");
        const std::string id = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

        CHECK(write_file(manifest::path(root, id),
                         "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
                         "Title: Quake III Arena\n"
                         "Version: 1.32\n"
                         "Actions:\n"
                         "- Name: Play\n"
                         "  IsPrimaryAction: true\n"));

        CHECK_EQ(run("$m = Get-GameManifest -Path '" + root + "' -Id '" + id + "'\n"
                     "$Return = $m.Title\n"),
                 "Quake III Arena");

        // Nested members survive the YAML -> object conversion.
        CHECK_EQ(run("$m = Get-GameManifest -Path '" + root + "' -Id '" + id + "'\n"
                     "$Return = $m.Actions[0].Name\n"),
                 "Play");

        // An absent manifest emits nothing rather than failing.
        const ScriptResult absent = run_full(
            "$m = Get-GameManifest -Path '" + root +
            "' -Id 'ffffffff-0000-0000-0000-000000000000'\n$Return = 'survived'\n");
        CHECK(absent.success);
        CHECK_EQ(absent.return_value, "survived");

        // Round trip: read, modify, write, read back.
        const ScriptResult round = run_full(
            "$m = Get-GameManifest -Path '" + root + "' -Id '" + id + "'\n"
            "$m.Title = 'Renamed'\n"
            "Write-GameManifest -Path '" + root + "' -Manifest $m | Out-Null\n"
            "$again = Get-GameManifest -Path '" + root + "' -Id '" + id + "'\n"
            "$Return = $again.Title\n");

        CHECK(round.success);
        CHECK_EQ(round.return_value, "Renamed");

        // And the file on disk is still YAML the .NET launcher would read.
        const std::string on_disk = read_file(manifest::path(root, id));
        CHECK(on_disk.find("Title: Renamed") != std::string::npos);
        CHECK(on_disk.find("{") == std::string::npos);

        path::remove_file(manifest::path(root, id));
    }

    // --- unregistering leaves the built-ins alone ---------------------------
    {
        cmdlets::unregister_all();

        const ScriptResult gone = run_full("$Return = Get-Runtime\n");
        CHECK(!gone.success);
        CHECK(gone.error.find("not recognized") != std::string::npos);

        const ScriptResult builtin = run_full("$Return = Join-Path 'a' 'b'\n");
        CHECK(builtin.success);

        // Leave the pack registered for anything that runs after this.
        cmdlets::register_all();
    }
}
