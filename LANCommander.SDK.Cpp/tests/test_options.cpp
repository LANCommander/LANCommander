#include "test_main.h"

#include "lancommander/manifest_helper.h"
#include "lancommander/script/cmdlets.h"
#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>

using namespace lancommander;

namespace {

const char* kGameId = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

std::string root_dir()
{
    return path::combine(path::temp_directory(), "lc_options_test");
}

bool write_manifest(const std::string& contents)
{
    const std::string target = manifest::path(root_dir(), kGameId);

    path::create_directories(path::parent(target));

    std::FILE* file = std::fopen(target.c_str(), "wb");
    if (!file)
        return false;
    std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);
    return true;
}

ScriptResult run_full(const std::string& source)
{
    PicoPoshScriptRunner runner;
    return runner.run_inline(source, "options-test", "", ScriptVariableList());
}

std::string run(const std::string& source)
{
    const ScriptResult r = run_full(source);
    if (!r.error.empty())
        std::printf("  (script error: %s)\n", r.error.c_str());
    return r.return_value;
}

// Every case reads the manifest at the same place, so keep the preamble in one
// spot.
std::string game_options(const std::string& tail)
{
    return run("$o = Get-GameOptions -Path '" + root_dir() + "' -Id '" +
               kGameId + "'\n" + tail);
}

std::string redist_options(const std::string& name, const std::string& tail)
{
    return run("$o = Get-RedistributableOptions -Path '" + root_dir() +
               "' -Id '" + kGameId + "' -Name '" + name + "'\n" + tail);
}

} // namespace

void test_options()
{
    cmdlets::register_all();

    // --- nested groups, straight from the umu-launcher example in
    //     .claude/skills/generate-redist-options/SKILL.md -------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "Title: Test Game\n"
            "OptionSchema: |\n"
            "  CommandTemplate: umu-run {exe} {args}\n"
            "  Options:\n"
            "    Game:\n"
            "      Description: Game identification\n"
            "      Options:\n"
            "        GAMEID:\n"
            "          Type: string\n"
            "          IsEnvironmentVariable: true\n"
            "          Default: umu-default\n"
            "    Proton:\n"
            "      Description: Proton configuration\n"
            "      Options:\n"
            "        PROTONPATH:\n"
            "          Type: string\n"
            "          Default: GE-Proton\n"));

        // Dot-notation keys become real nesting.
        CHECK_EQ(game_options("$Return = $o.Game.GAMEID\n"), "umu-default");
        CHECK_EQ(game_options("$Return = $o.Proton.PROTONPATH\n"), "GE-Proton");

        // Group nodes carry no Type, so they are not themselves options —
        // only a container for their children.
        CHECK_EQ(game_options("$Return = ($o.Game | ConvertTo-Json)\n"),
                 "{\"GAMEID\":\"umu-default\"}");
    }

    // --- per-entity Options override the schema defaults --------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    Proton:\n"
            "      Options:\n"
            "        PROTONPATH:\n"
            "          Type: string\n"
            "          Default: GE-Proton\n"
            "        VERB:\n"
            "          Type: string\n"
            "          Default: waitforexitandrun\n"
            "Options:\n"
            "  Proton.PROTONPATH: GE-Proton9-20\n"
            "  Extra.Unschemed: kept\n"));

        CHECK_EQ(game_options("$Return = $o.Proton.PROTONPATH\n"), "GE-Proton9-20");
        // An option the override does not mention keeps its default.
        CHECK_EQ(game_options("$Return = $o.Proton.VERB\n"), "waitforexitandrun");
        // An override with no matching schema entry is still surfaced.
        CHECK_EQ(game_options("$Return = $o.Extra.Unschemed\n"), "kept");
    }

    // --- scalar list, from the skill's example ------------------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    AllowedHosts:\n"
            "      Type: list\n"
            "      ItemType: string\n"
            "      Default:\n"
            "        - localhost\n"
            "        - example.com\n"
            "    Ports:\n"
            "      Type: list\n"
            "      ItemType: int\n"
            "      Default:\n"
            "        - 7777\n"
            "        - 7778\n"));

        // A list default is stored as JSON and hydrated back into an array,
        // not left as a string.
        CHECK_EQ(game_options("$Return = $o.AllowedHosts[0]\n"), "localhost");
        CHECK_EQ(game_options("$Return = $o.AllowedHosts[1]\n"), "example.com");
        CHECK_EQ(game_options("$Return = $o.AllowedHosts.Count\n"), "2");

        // ItemType: int means arithmetic works rather than concatenation.
        CHECK_EQ(game_options("$Return = $o.Ports[0] + 1\n"), "7778");
    }

    // --- composite list, from the skill's master-server example -------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    MasterServers:\n"
            "      Type: list\n"
            "      DisplayName: Master Servers\n"
            "      Fields:\n"
            "        Address:\n"
            "          Type: string\n"
            "          Default: master.example.com\n"
            "        Port:\n"
            "          Type: int\n"
            "          Default: 28900\n"
            "        GameName:\n"
            "          Type: string\n"
            "          Default: unreal\n"
            "      MinItems: 1\n"
            "      Default:\n"
            "        - { Address: master.oldunreal.com, Port: 28900, GameName: unreal }\n"
            "        - { Address: master.hlkclan.net, GameName: unreal }\n"));

        CHECK_EQ(game_options("$Return = $o.MasterServers.Count\n"), "2");
        CHECK_EQ(game_options("$Return = $o.MasterServers[0].Address\n"),
                 "master.oldunreal.com");
        CHECK_EQ(game_options("$Return = $o.MasterServers[1].Address\n"),
                 "master.hlkclan.net");

        // Port is typed int by its field definition.
        CHECK_EQ(game_options("$Return = $o.MasterServers[0].Port + 1\n"), "28901");

        // The second row omits Port, so the field's own default fills it in.
        CHECK_EQ(game_options("$Return = $o.MasterServers[1].Port\n"), "28900");

        // A list's Fields describe per-item shape, not sibling options, so
        // they must not appear as options in their own right.
        CHECK_EQ(game_options("$Return = ($o.MasterServers[0] | ConvertTo-Json)\n"),
                 "{\"Address\":\"master.oldunreal.com\",\"Port\":28900,"
                 "\"GameName\":\"unreal\"}");
    }

    // --- a list is a leaf: its children are not sibling options -------------
    //
    // OptionSchema.FlattenOptions guards this explicitly — a list's shape
    // describes its items, not options alongside it. Without the guard,
    // "Servers.Address" would appear as an option of its own and clobber the
    // hydrated array.
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    Servers:\n"
            "      Type: list\n"
            "      ItemType: string\n"
            "      Default:\n"
            "        - one\n"
            "      Options:\n"
            "        Nested:\n"
            "          Type: string\n"
            "          Default: should-not-appear\n"));

        // Servers stays the hydrated array...
        CHECK_EQ(game_options("$Return = $o.Servers[0]\n"), "one");
        // ...and the nested option under it was never flattened.
        CHECK_EQ(game_options("$Return = ($o | ConvertTo-Json)\n"),
                 "{\"Servers\":[\"one\"]}");
    }

    // --- a list overridden by the per-entity Options ------------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    AllowedHosts:\n"
            "      Type: list\n"
            "      ItemType: string\n"
            "      Default:\n"
            "        - localhost\n"
            "Options:\n"
            "  AllowedHosts: '[\"a.example.com\",\"b.example.com\"]'\n"));

        CHECK_EQ(game_options("$Return = $o.AllowedHosts.Count\n"), "2");
        CHECK_EQ(game_options("$Return = $o.AllowedHosts[1]\n"), "b.example.com");
    }

    // A stored list value that is not valid JSON is surfaced raw rather than
    // throwing inside the script.
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options:\n"
            "    AllowedHosts:\n"
            "      Type: list\n"
            "      ItemType: string\n"
            "Options:\n"
            "  AllowedHosts: 'not json at all'\n"));

        CHECK_EQ(game_options("$Return = $o.AllowedHosts\n"), "not json at all");
    }

    // --- no schema at all ---------------------------------------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "Title: No Options Here\n"));

        const ScriptResult r = run_full(
            "$o = Get-GameOptions -Path '" + root_dir() + "' -Id '" + kGameId +
            "'\n$Return = ($o | ConvertTo-Json)\n");

        CHECK(r.success);
        CHECK_EQ(r.return_value, "{}");
    }

    // --- Get-RedistributableOptions -----------------------------------------
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "Redistributables:\n"
            "- Id: 11111111-0000-0000-0000-000000000001\n"
            "  Name: DirectPlay\n"
            "  OptionSchema: |\n"
            "    Options:\n"
            "      Compatibility:\n"
            "        Options:\n"
            "          Layer:\n"
            "            Type: string\n"
            "            Default: WIN98\n"
            "  Options:\n"
            "    Compatibility.Layer: WINXPSP3\n"
            "- Id: 11111111-0000-0000-0000-000000000002\n"
            "  Name: VCRedist\n"
            "  OptionSchema: |\n"
            "    Options:\n"
            "      Arch:\n"
            "        Type: string\n"
            "        Default: x86\n"));

        // The named redistributable's own schema and overrides are used.
        CHECK_EQ(redist_options("DirectPlay", "$Return = $o.Compatibility.Layer\n"),
                 "WINXPSP3");
        CHECK_EQ(redist_options("VCRedist", "$Return = $o.Arch\n"), "x86");

        // Matching is case-insensitive, as in .NET.
        CHECK_EQ(redist_options("vcredist", "$Return = $o.Arch\n"), "x86");

        // A name that is not in the manifest is an error, not an empty object.
        {
            const ScriptResult r = run_full(
                "$o = Get-RedistributableOptions -Path '" + root_dir() +
                "' -Id '" + kGameId + "' -Name 'NoSuchThing'\n");

            CHECK(!r.success);
            CHECK(r.error.find("not found in manifest") != std::string::npos);
        }
    }

    // --- error cases --------------------------------------------------------
    {
        const ScriptResult missing = run_full(
            "$o = Get-GameOptions -Path '" + root_dir() +
            "' -Id 'ffffffff-0000-0000-0000-000000000000'\n");
        CHECK(!missing.success);
        CHECK(missing.error.find("could not read the game manifest") !=
              std::string::npos);

        const ScriptResult no_path = run_full("$o = Get-GameOptions\n");
        CHECK(!no_path.success);
        CHECK(no_path.error.find("requires a -Path") != std::string::npos);

        const ScriptResult no_name = run_full(
            "$o = Get-RedistributableOptions -Path '" + root_dir() +
            "' -Id '" + kGameId + "'\n");
        CHECK(!no_name.success);
        CHECK(no_name.error.find("requires a -Name") != std::string::npos);
    }

    // A schema that is not valid YAML reports rather than emitting nothing.
    {
        CHECK(write_manifest(
            "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
            "OptionSchema: |\n"
            "  Options: [unterminated\n"));

        const ScriptResult r = run_full(
            "$o = Get-GameOptions -Path '" + root_dir() + "' -Id '" + kGameId + "'\n");

        CHECK(!r.success);
        CHECK(r.error.find("could not parse the option schema") != std::string::npos);
    }

    path::remove_file(manifest::path(root_dir(), kGameId));
}
