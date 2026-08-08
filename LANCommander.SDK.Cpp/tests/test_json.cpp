#include "test_main.h"

#include "lancommander/clients/game_client.h"

#include "json/json_helpers.h"
#include "json/json_writer.h"

#include <cstdio>

using namespace lancommander;

namespace {

Script parse_script_text(const std::string& text)
{
    json::JsonDoc doc(text);
    if (!doc)
        return Script();
    return json::parse_script(doc.root);
}

std::string ordinal_script(int ordinal)
{
    char buffer[128];
    std::sprintf(buffer, "{\"Type\":%d,\"Name\":\"x\",\"Contents\":\"y\"}", ordinal);
    return buffer;
}

} // namespace

void test_json()
{
    // --- every ScriptType ordinal round-trips ------------------------------
    //
    // Regression: the switch used to stop at 10, so Package (14) and
    // RunWrapper (15) parsed as Unknown — and ScriptClient drops Unknown
    // scripts, which made both types invisible to the C++ SDK entirely.
    const ScriptType expected[16] = {
        ScriptType::Install, ScriptType::Uninstall, ScriptType::NameChange,
        ScriptType::KeyChange, ScriptType::SaveUpload, ScriptType::SaveDownload,
        ScriptType::DetectInstall, ScriptType::BeforeStart, ScriptType::AfterStop,
        ScriptType::GameStarted, ScriptType::GameStopped, ScriptType::UserRegistration,
        ScriptType::UserLogin, ScriptType::ApplicationStart, ScriptType::Package,
        ScriptType::RunWrapper
    };

    for (int i = 0; i < 16; ++i)
        CHECK(parse_script_text(ordinal_script(i)).type == expected[i]);

    CHECK(parse_script_text(ordinal_script(99)).type == ScriptType::Unknown);

    // --- the same names, as strings ----------------------------------------
    CHECK(parse_script_text("{\"Type\":\"Package\"}").type == ScriptType::Package);
    CHECK(parse_script_text("{\"Type\":\"RunWrapper\"}").type == ScriptType::RunWrapper);
    CHECK(parse_script_text("{\"Type\":\"DetectInstall\"}").type == ScriptType::DetectInstall);
    CHECK(parse_script_text("{\"Type\":\"GameStopped\"}").type == ScriptType::GameStopped);
    CHECK(parse_script_text("{\"Type\":\"Nonsense\"}").type == ScriptType::Unknown);

    // --- the fields added alongside the runner -----------------------------
    {
        const Script s = parse_script_text(
            "{\"Type\":0,\"Name\":\"Install\",\"Description\":\"d\","
            "\"Contents\":\"c\",\"RequiresAdmin\":true,\"Platforms\":3}");

        CHECK_EQ(s.name, "Install");
        CHECK_EQ(s.description, "d");
        CHECK_EQ(s.contents, "c");
        CHECK(s.requires_admin);
        CHECK(s.platforms == (RuntimePlatform_Windows | RuntimePlatform_Linux));
    }

    // Platforms may arrive as an ordinal, a comma-separated name list, or an
    // array. Getting this wrong fails open (None means "runs everywhere"), so
    // all three shapes are handled.
    CHECK(parse_script_text("{\"Platforms\":1}").platforms == RuntimePlatform_Windows);
    CHECK(parse_script_text("{\"Platforms\":\"Windows, Linux\"}").platforms ==
          (RuntimePlatform_Windows | RuntimePlatform_Linux));
    CHECK(parse_script_text("{\"Platforms\":\"macOS\"}").platforms == RuntimePlatform_macOS);
    CHECK(parse_script_text("{\"Platforms\":[\"Windows\",\"macOS\"]}").platforms ==
          (RuntimePlatform_Windows | RuntimePlatform_macOS));
    CHECK(parse_script_text("{\"Name\":\"x\"}").platforms == RuntimePlatform_None);

    // --- camelCase is accepted too -----------------------------------------
    {
        const Script s = parse_script_text(
            "{\"type\":6,\"name\":\"n\",\"requiresAdmin\":true,\"platforms\":2}");
        CHECK(s.type == ScriptType::DetectInstall);
        CHECK(s.requires_admin);
        CHECK(s.platforms == RuntimePlatform_Linux);
    }

    // --- manifest custom fields and scripts are parsed ---------------------
    {
        GameManifest manifest;
        std::string error;

        const bool parsed = parse_manifest_json(
            "{\"Id\":\"g1\",\"Title\":\"Quake\",\"Version\":\"1.32\","
            "\"CustomFields\":[{\"Name\":\"Port\",\"Value\":\"27960\"}],"
            "\"Scripts\":[{\"Type\":0,\"Platforms\":1}]}",
            &manifest, &error);

        CHECK(parsed);
        CHECK_EQ(manifest.title, "Quake");
        CHECK(manifest.custom_fields.size() == 1);
        if (manifest.custom_fields.size() == 1) {
            CHECK_EQ(manifest.custom_fields[0].name, "Port");
            CHECK_EQ(manifest.custom_fields[0].value, "27960");
        }
        CHECK(manifest.scripts.size() == 1);
        if (manifest.scripts.size() == 1) {
            CHECK(manifest.scripts[0].type == ScriptType::Install);
            CHECK(manifest.scripts[0].platforms == RuntimePlatform_Windows);
        }
    }

    // --- redistributables carry their scripts ------------------------------
    {
        json::JsonDoc doc("{\"Id\":\"r1\",\"Name\":\"DirectX\","
                          "\"Scripts\":[{\"Type\":6},{\"Type\":0}]}");
        CHECK(static_cast<bool>(doc));
        if (doc) {
            const Redistributable r = json::parse_redistributable(doc.root);
            CHECK_EQ(r.name, "DirectX");
            CHECK(r.scripts.size() == 2);
            if (r.scripts.size() == 2) {
                CHECK(r.scripts[0].type == ScriptType::DetectInstall);
                CHECK(r.scripts[1].type == ScriptType::Install);
            }
        }
    }

    // --- the writer round-trips through the parser -------------------------
    {
        GameManifest manifest;
        manifest.id = "g1";
        manifest.title = "Quake III \"Arena\"";   // quotes must survive escaping
        manifest.version = "1.32";

        GameCustomField field;
        field.name = "Port";
        field.value = "27960";
        manifest.custom_fields.push_back(field);

        Script s;
        s.type = ScriptType::RunWrapper;
        s.name = "wrapper";
        s.requires_admin = true;
        s.platforms = RuntimePlatform_Windows | RuntimePlatform_Linux;
        manifest.scripts.push_back(s);

        const std::string text = json::serialize_game_manifest(manifest);

        GameManifest parsed;
        std::string error;
        CHECK(parse_manifest_json(text, &parsed, &error));
        CHECK_EQ(parsed.title, manifest.title);
        CHECK_EQ(parsed.version, manifest.version);
        CHECK(parsed.custom_fields.size() == 1);
        CHECK(parsed.scripts.size() == 1);
        if (parsed.scripts.size() == 1) {
            CHECK(parsed.scripts[0].type == ScriptType::RunWrapper);
            CHECK(parsed.scripts[0].requires_admin);
            CHECK(parsed.scripts[0].platforms ==
                  (RuntimePlatform_Windows | RuntimePlatform_Linux));
        }
    }
}
