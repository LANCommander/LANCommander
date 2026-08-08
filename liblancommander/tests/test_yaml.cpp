#include "test_main.h"

#include "lancommander/clients/game_client.h"
#include "lancommander/manifest_helper.h"
#include "lancommander/util/path.h"

#include "yaml/yaml_convert.h"

#include <cstdio>

using namespace lancommander;

namespace {

// Shaped the way YamlDotNet's SerializerBuilder with PascalCaseNamingConvention
// emits a game manifest: block style, plain scalars, PascalCase keys.
const char* kManifest =
    "Id: 3f2504e0-4f89-11d3-9a0c-0305e82c3301\n"
    "Title: Quake III Arena\n"
    "SortTitle: Quake 3 Arena\n"
    "Version: 1.32\n"
    "ReleasedOn: 1999-12-02\n"
    "Actions:\n"
    "- Name: Play\n"
    "  Path: \"{InstallDir}\\\\quake3.exe\"\n"
    "  Arguments: +set fs_game baseq3\n"
    "  WorkingDirectory: \"\"\n"
    "  IsPrimaryAction: true\n"
    "  SortOrder: 0\n"
    "- Name: Dedicated Server\n"
    "  Path: \"{InstallDir}\\\\quake3.exe\"\n"
    "  IsPrimaryAction: false\n"
    "  SortOrder: 1\n"
    "SavePaths:\n"
    "- Id: 8a1b2c3d-0000-0000-0000-000000000001\n"
    "  Path: baseq3/q3config.cfg\n"
    "  IsFile: true\n"
    "  IsRegex: false\n"
    "CustomFields:\n"
    "- Name: Port\n"
    "  Value: 27960\n"
    "- Name: Notes\n"
    "  Value: \"true\"\n"
    "Scripts:\n"
    "- Type: 0\n"
    "  Name: Install\n"
    "  RequiresAdmin: true\n"
    "  Platforms: 1\n";

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

bool write_file(const std::string& path, const std::string& contents)
{
    path::create_directories(path::parent(path));

    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return false;
    std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);
    return true;
}

} // namespace

void test_yaml()
{
    // --- scalar typing ------------------------------------------------------
    //
    // A plain YAML scalar carries no type, so it is inferred. A *quoted* one is
    // always a string — which is how "Notes: \"true\"" above stays text.
    CHECK_EQ(yaml::to_json("a: 1\n").value, "{\"a\":1}");
    CHECK_EQ(yaml::to_json("a: 1.5\n").value, "{\"a\":1.5}");
    CHECK_EQ(yaml::to_json("a: true\n").value, "{\"a\":true}");
    CHECK_EQ(yaml::to_json("a: false\n").value, "{\"a\":false}");
    CHECK_EQ(yaml::to_json("a: null\n").value, "{\"a\":null}");
    CHECK_EQ(yaml::to_json("a: ~\n").value, "{\"a\":null}");
    CHECK_EQ(yaml::to_json("a: hello\n").value, "{\"a\":\"hello\"}");
    CHECK_EQ(yaml::to_json("a: \"1\"\n").value, "{\"a\":\"1\"}");
    CHECK_EQ(yaml::to_json("a: 'true'\n").value, "{\"a\":\"true\"}");
    CHECK_EQ(yaml::to_json("a: 1.32\n").value, "{\"a\":1.32}");

    // --- structure ----------------------------------------------------------
    CHECK_EQ(yaml::to_json("- 1\n- 2\n").value, "[1,2]");
    CHECK_EQ(yaml::to_json("a:\n  b:\n    c: deep\n").value,
             "{\"a\":{\"b\":{\"c\":\"deep\"}}}");
    CHECK_EQ(yaml::to_json("a: [1, 2]\n").value, "{\"a\":[1,2]}");   // flow style
    CHECK_EQ(yaml::to_json("# just a comment\na: 1\n").value, "{\"a\":1}");

    // --- errors are reported, not guessed ----------------------------------
    {
        Result<std::string> aliased = yaml::to_json("a: &x 1\nb: *x\n");
        CHECK(!aliased.success);
        CHECK(aliased.error.find("anchors") != std::string::npos);
    }
    {
        Result<std::string> broken = yaml::to_json("a: [1, 2\n");
        CHECK(!broken.success);
    }

    // --- a realistic manifest parses into the model -------------------------
    {
        Result<std::string> json = yaml::to_json(kManifest);
        CHECK(json.success);

        GameManifest parsed;
        std::string error;
        CHECK(parse_manifest_json(json.value, &parsed, &error));

        CHECK_EQ(parsed.title, "Quake III Arena");
        CHECK_EQ(parsed.version, "1.32");
        CHECK(parsed.actions.size() == 2);
        if (parsed.actions.size() == 2) {
            CHECK_EQ(parsed.actions[0].name, "Play");
            CHECK(parsed.actions[0].is_primary);
            CHECK(!parsed.actions[1].is_primary);
            CHECK(parsed.actions[1].sort_order == 1);
        }
        CHECK(parsed.save_paths.size() == 1);
        if (parsed.save_paths.size() == 1)
            CHECK_EQ(parsed.save_paths[0].path, "baseq3/q3config.cfg");

        CHECK(parsed.custom_fields.size() == 2);
        if (parsed.custom_fields.size() == 2) {
            // "Value: 27960" is a plain scalar, so it types as a JSON number,
            // but the model stores custom-field values as strings — the
            // accessors stringify rather than dropping it. Same reason
            // "Version: 1.32" above survives as a string.
            CHECK_EQ(parsed.custom_fields[0].name, "Port");
            CHECK_EQ(parsed.custom_fields[0].value, "27960");
            CHECK_EQ(parsed.custom_fields[1].value, "true");
        }

        CHECK(parsed.scripts.size() == 1);
        if (parsed.scripts.size() == 1) {
            CHECK(parsed.scripts[0].type == ScriptType::Install);
            CHECK(parsed.scripts[0].requires_admin);
            CHECK(parsed.scripts[0].platforms == RuntimePlatform_Windows);
        }
    }

    // --- YAML -> JSON -> YAML -> JSON is stable ----------------------------
    {
        Result<std::string> json = yaml::to_json(kManifest);
        CHECK(json.success);

        Result<std::string> back = yaml::from_json(json.value);
        CHECK(back.success);

        Result<std::string> again = yaml::to_json(back.value);
        CHECK(again.success);
        CHECK_EQ(again.value, json.value);
    }

    // A string that would read back as a number or bool must survive.
    {
        Result<std::string> yaml_text =
            yaml::from_json("{\"a\":\"1\",\"b\":\"true\",\"c\":\"null\",\"d\":\"\"}");
        CHECK(yaml_text.success);

        Result<std::string> round = yaml::to_json(yaml_text.value);
        CHECK(round.success);
        CHECK_EQ(round.value, "{\"a\":\"1\",\"b\":\"true\",\"c\":\"null\",\"d\":\"\"}");
    }

    // --- manifest_helper reads and writes the shared location --------------
    {
        const std::string root =
            path::combine(path::temp_directory(), "lc_manifest_test");
        const std::string entity = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

        const std::string target = manifest::path(root, entity);
        CHECK(target.find("Manifest.yml") != std::string::npos);
        CHECK(target.find(".lancommander") != std::string::npos);

        CHECK(write_file(target, kManifest));
        CHECK(manifest::exists(root, entity));

        Result<GameManifest> loaded = manifest::read(root, entity);
        CHECK(loaded.success);
        if (loaded.success)
            CHECK_EQ(loaded.value.title, "Quake III Arena");

        // read_json keeps fields the struct does not model — this is what gets
        // injected as $GameManifest.
        Result<std::string> raw = manifest::read_json(target);
        CHECK(raw.success);
        if (raw.success) {
            CHECK(raw.value.find("SortTitle") != std::string::npos);
            CHECK(raw.value.find("ReleasedOn") != std::string::npos);
        }

        // Writing produces YAML a second read understands.
        GameManifest fresh;
        fresh.id = entity;
        fresh.title = "Written By C++";
        fresh.version = "2.0";

        Result<std::string> written = manifest::write(fresh, root, entity);
        CHECK(written.success);

        Result<GameManifest> reloaded = manifest::read(root, entity);
        CHECK(reloaded.success);
        if (reloaded.success) {
            CHECK_EQ(reloaded.value.title, "Written By C++");
            CHECK_EQ(reloaded.value.version, "2.0");
        }

        path::remove_file(target);
    }
}
