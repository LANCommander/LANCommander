#include "test_main.h"

#include "lancommander/script/cmdlets.h"
#include "lancommander/script/picoposh_script_runner.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>

using namespace lancommander;

namespace {

const char* kGameSpyHost = "gamespy.com";
const char* kOpenSpyHost = "openspy.net";

const char* kGameSpyKey =
    "BF05D63E93751AD4A59A4A7389CF0BE8A22CCDEEA1E7F12C062D6E194472EFDA"
    "5184CCECEB4FBADF5EB1D7ABFE91181453972AA971F624AF9BA8F0F82E2869FB"
    "7D44BDE8D56EE50977898F3FEE75869622C4981F07506248BD3D092E8EA05C12"
    "B2FA37881176084C8F8B8756C4722CDC57D2AD28ACD3AD85934FB48D6B2D2027";

const char* kOpenSpyKey =
    "afb5818995b3708d0656a5bdd20760aee76537907625f6d23f40bf17029e5680"
    "8d36966c0804e1d797e310fedd8c06e6c4121d963863d765811fc9baeb2315c9"
    "a6eaeb125fad694d9ea4d4a928f223d9f4514533f18a5432dd0435c5c6ac8e27"
    "6cf29489cb5ac880f16b0d7832ee927d4e27d622d6a450cd1560d7fa882c6c13";

std::string root_dir()
{
    return path::combine(path::temp_directory(), "lc_gamespy_test");
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
    char buffer[4096];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        out.append(buffer, read);
    std::fclose(file);
    return out;
}

void reset_root()
{
    // Leave no files behind between cases; the glob walks whatever is there.
    Result<std::vector<path::DirectoryEntry> > entries =
        path::list_directory(root_dir());

    if (entries) {
        for (std::size_t i = 0; i < entries.value.size(); ++i) {
            if (!entries.value[i].is_directory)
                path::remove_file(path::combine(root_dir(), entries.value[i].name));
        }
    }

    path::create_directories(root_dir());
}

ScriptResult run_full(const std::string& source)
{
    PicoPoshScriptRunner runner;
    return runner.run_inline(source, "gamespy-test", "", ScriptVariableList());
}

ScriptResult patch(const std::string& extra_args = std::string())
{
    return run_full("Edit-PatchGameSpy -Path '" + root_dir() + "'" +
                    extra_args + "\n");
}

bool contains(const std::string& haystack, const std::string& needle)
{
    return haystack.find(needle) != std::string::npos;
}

} // namespace

void test_gamespy()
{
    cmdlets::register_all();
    path::create_directories(root_dir());

    // --- binary: the hostname is replaced in place --------------------------
    {
        reset_root();

        // Padding either side so a wrong offset or a length change is visible.
        const std::string before =
            std::string("HEADER\x00\x01", 8) + kGameSpyHost +
            std::string("\x00TRAILER", 8);
        const std::string target = path::combine(root_dir(), "game.exe");
        CHECK(write_file(target, before));

        const ScriptResult r = patch();
        CHECK(r.success);

        const std::string after = read_file(target);
        // Same length: the replacement is byte-for-byte, so nothing shifts.
        CHECK(after.size() == before.size());
        CHECK(contains(after, kOpenSpyHost));
        CHECK(!contains(after, kGameSpyHost));
        CHECK(contains(after, "HEADER"));
        CHECK(contains(after, "TRAILER"));
    }

    // Every occurrence is patched, not just the first.
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "multi.dll");
        CHECK(write_file(target, std::string("a") + kGameSpyHost + "b" +
                                 kGameSpyHost + "c"));

        CHECK(patch().success);

        const std::string after = read_file(target);
        CHECK(!contains(after, kGameSpyHost));
        CHECK_EQ(after, std::string("a") + kOpenSpyHost + "b" + kOpenSpyHost + "c");
    }

    // A match spanning the internal read boundary is still found. The chunk is
    // 8 KiB, so straddle it deliberately.
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "boundary.dll");
        std::string padding(8190, 'x');
        CHECK(write_file(target, padding + kGameSpyHost + "tail"));

        CHECK(patch().success);

        const std::string after = read_file(target);
        CHECK(!contains(after, kGameSpyHost));
        CHECK(contains(after, kOpenSpyHost));
    }

    // --- binary: the public key ---------------------------------------------
    //
    // The .NET version searches for the OpenSpy key rather than the GameSpy
    // one, so it never patches an unpatched binary. We search for both.
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "keys.dll");
        CHECK(write_file(target, std::string("pre") + kGameSpyKey + "post"));

        CHECK(patch().success);

        const std::string after = read_file(target);
        CHECK(!contains(after, kGameSpyKey));
        CHECK(contains(after, kOpenSpyKey));
    }

    // With a custom key, an already-patched binary is re-pointed too.
    {
        reset_root();

        std::string custom(256, 'a');
        const std::string target = path::combine(root_dir(), "repoint.dll");
        CHECK(write_file(target, std::string("pre") + kOpenSpyKey + "post"));

        CHECK(patch(" -PublicKey '" + custom + "'").success);

        const std::string after = read_file(target);
        CHECK(contains(after, custom));
        CHECK(!contains(after, kOpenSpyKey));
    }

    // --- length validation --------------------------------------------------
    //
    // "gamespy.com" is 11 characters; the .NET help text and the scripting
    // docs both say 12, but the code compares against the real length.
    {
        reset_root();

        const ScriptResult short_host = patch(" -Hostname 'short.io'");
        CHECK(!short_host.success);
        CHECK(contains(short_host.error, "exactly 11 characters"));

        const ScriptResult bad_key = patch(" -PublicKey 'tooshort'");
        CHECK(!bad_key.success);
        CHECK(contains(bad_key.error, "exactly 256 characters"));

        // An 11-character replacement is accepted.
        CHECK(patch(" -Hostname 'example.org'").success);
    }

    // --- text: UT99 master server address -----------------------------------
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "UnrealTournament.ini");
        CHECK(write_file(target,
                         "[UBrowserAll]\r\n"
                         "MasterServerAddress=master0.gamespy.com\r\n"
                         "bFallbackFactories=True\r\n"
                         "\r\n"
                         "[Engine.GameEngine]\r\n"
                         "MasterServerAddress   =   master0.gamespy.com\r\n"));

        CHECK(patch().success);

        const std::string after = read_file(target);
        // Both occurrences, including the one with padded whitespace.
        CHECK(!contains(after, "gamespy.com"));
        CHECK(contains(after, "MasterServerAddress=master.openspy.net"));
        // bFallbackFactories inside [UBrowserAll] is flipped...
        CHECK(contains(after, "bFallbackFactories=False"));
        // ...and the surrounding structure survives.
        CHECK(contains(after, "[Engine.GameEngine]"));
    }

    // bFallbackFactories outside [UBrowserAll] is left alone — the .NET pattern
    // is scoped to that block.
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "scoped.ini");
        CHECK(write_file(target,
                         "[UBrowserAll]\r\n"
                         "Something=1\r\n"
                         "\r\n"
                         "[Other]\r\n"
                         "bFallbackFactories=True\r\n"));

        CHECK(patch().success);

        CHECK(contains(read_file(target), "bFallbackFactories=True"));
    }

    // --- text: Unreal 2 master server list ----------------------------------
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "UT2004.ini");
        CHECK(write_file(target,
                         "[IpDrv.MasterServerLink]\r\n"
                         "MasterServerList=(Address=\"ut2004master1.epicgames.com\",Port=28902)\r\n"
                         "MasterServerList=(Address=\"ut2004master2.epicgames.com\",Port=28902)\r\n"
                         "MasterServerList=(Address=\"ut2003master.epicgames.com\",Port=28902)\r\n"
                         "[NextSection]\r\n"
                         "Keep=1\r\n"));

        CHECK(patch().success);

        const std::string after = read_file(target);

        // The whole run collapses to one entry...
        CHECK(contains(after,
                       "MasterServerList=(Address=\"utmaster.openspy.net\",Port=28902)"));
        CHECK(!contains(after, "epicgames.com"));

        // ...the header is preserved, and what follows is untouched.
        CHECK(contains(after, "[IpDrv.MasterServerLink]"));
        CHECK(contains(after, "[NextSection]"));
        CHECK(contains(after, "Keep=1"));
    }

    // --- glob behaviour -----------------------------------------------------
    //
    // The defaults are non-recursive, matching FileSystemGlobbing: "*.dll"
    // covers the base directory only.
    {
        reset_root();

        const std::string nested =
            path::combine(path::combine(root_dir(), "System"), "deep.dll");
        CHECK(write_file(nested, std::string("x") + kGameSpyHost + "y"));

        CHECK(patch().success);
        // Untouched: no "**" in the default patterns.
        CHECK(contains(read_file(nested), kGameSpyHost));

        // With "**" it is found.
        CHECK(patch(" -BinariesToPatch '**/*.dll'").success);
        CHECK(contains(read_file(nested), kOpenSpyHost));

        path::remove_file(nested);
    }

    // An explicit list of patterns binds as an array.
    {
        reset_root();

        const std::string exe = path::combine(root_dir(), "a.exe");
        const std::string so = path::combine(root_dir(), "b.so");
        CHECK(write_file(exe, std::string("p") + kGameSpyHost));
        CHECK(write_file(so, std::string("q") + kGameSpyHost));

        CHECK(patch(" -BinariesToPatch @('*.exe','*.so')").success);

        CHECK(contains(read_file(exe), kOpenSpyHost));
        CHECK(contains(read_file(so), kOpenSpyHost));
    }

    // picoposh does not parse `-Param a,b`: the comma becomes its own argument
    // and would otherwise land on -Hostname. The scripting docs show that form,
    // so the error has to point at the array syntax rather than complain about
    // a hostname length.
    {
        reset_root();

        const ScriptResult r = patch(" -BinariesToPatch '*.exe','*.so'");

        CHECK(!r.success);
        CHECK(contains(r.error, "bare comma list is not supported"));
        CHECK(contains(r.error, "@('*.dll','*.exe')"));
    }

    // --- files with nothing to patch are left byte-identical ----------------
    {
        reset_root();

        const std::string target = path::combine(root_dir(), "innocent.ini");
        const std::string before = "[Something]\r\nKey=Value\r\n";
        CHECK(write_file(target, before));

        CHECK(patch().success);
        CHECK_EQ(read_file(target), before);
    }

    // --- a missing directory reports ----------------------------------------
    {
        const ScriptResult r = run_full(
            "Edit-PatchGameSpy -Path '" +
            path::combine(path::temp_directory(), "lc_no_such_dir_4b2c") + "'\n");

        CHECK(!r.success);
        CHECK(contains(r.error, "not a directory"));
    }

    reset_root();
}
