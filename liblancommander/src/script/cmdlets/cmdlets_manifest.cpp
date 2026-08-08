// Get-GameManifest, Write-GameManifest, New-Package.

#include "script/cmdlets/cmdlet_defs.h"

#include "lancommander/manifest_helper.h"
#include "lancommander/util/path.h"

#include "cJSON.h"
#include "json/json_writer.h"
#include "yaml/yaml_convert.h"

#include <cstdio>
#include <string>

namespace lancommander {
namespace cmdlets {

namespace {

// --- Get-GameManifest -----------------------------------------------------

pico_status get_game_manifest_begin(PicoStage* ctx)
{
    std::string directory;
    std::string id;

    if (!arg_string(ctx, "Path", 0, &directory))
        return fail(ctx, PICO_ERR_ARG, "requires a -Path");
    if (!arg_string(ctx, "Id", -1, &id))
        return fail(ctx, PICO_ERR_ARG, "requires an -Id");

    const std::string manifest_path = manifest::path(directory, id);
    if (!path::exists(manifest_path)) {
        // ManifestHelper.Read returns null when the file is absent rather than
        // throwing, so emit nothing and let the script decide.
        return PICO_OK;
    }

    // Emitted from the raw YAML rather than a parsed GameManifest, so every
    // field survives — scripts reach well past what the struct models.
    Result<std::string> json = manifest::read_json(manifest_path);
    if (!json)
        return fail(ctx, PICO_ERR_IO, json.error);

    return emit_json(ctx, json.value);
}

const PicoParamDef get_game_manifest_params[] = {
    { "Path", 0, 0 }, { "Id", 0, -1 }
};

const PicoCmdletDef get_game_manifest_def = {
    "Get-GameManifest", get_game_manifest_params, 2,
    get_game_manifest_begin, NULL, NULL,
    "Reads Manifest.yml for a game from an install directory."
};

// --- Write-GameManifest ---------------------------------------------------

pico_status write_game_manifest_begin(PicoStage* ctx)
{
    std::string directory;
    if (!arg_string(ctx, "Path", 0, &directory))
        return fail(ctx, PICO_ERR_ARG, "requires a -Path");

    pico_value manifest_value;
    if (!pico_bp_named(&ctx->bp, "Manifest", &manifest_value) &&
        !pico_bp_pos(&ctx->bp, 1, &manifest_value))
        return fail(ctx, PICO_ERR_ARG, "requires a -Manifest");

    const std::string json = value_to_json(manifest_value);
    pico_val_release(manifest_value);

    // The id comes from the manifest itself, matching ManifestHelper.Write.
    std::string id;
    {
        cJSON* parsed = cJSON_Parse(json.c_str());
        if (parsed) {
            cJSON* id_node = cJSON_GetObjectItem(parsed, "Id");
            if (!id_node)
                id_node = cJSON_GetObjectItem(parsed, "id");
            if (id_node && id_node->valuestring)
                id = id_node->valuestring;
            cJSON_Delete(parsed);
        }
    }

    if (id.empty())
        return fail(ctx, PICO_ERR_ARG, "the manifest has no Id");

    Result<std::string> written =
        manifest::write_json(manifest::path(directory, id), json);
    if (!written)
        return fail(ctx, PICO_ERR_IO, written.error);

    return emit_string(ctx, written.value);
}

const PicoParamDef write_game_manifest_params[] = {
    { "Path", 0, 0 }, { "Manifest", 0, 1 }
};

const PicoCmdletDef write_game_manifest_def = {
    "Write-GameManifest", write_game_manifest_params, 2,
    write_game_manifest_begin, NULL, NULL,
    "Writes a manifest object to Manifest.yml and returns the path written."
};

// --- New-Package ----------------------------------------------------------

pico_status new_package_begin(PicoStage* ctx)
{
    std::string package_path;
    std::string version;
    std::string changelog;

    if (!arg_string(ctx, "Path", 0, &package_path) || package_path.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Path");
    if (!arg_string(ctx, "Version", 1, &version) || version.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Version");

    arg_string(ctx, "Changelog", 2, &changelog);

    PicoObject* package = pico_object_new();
    if (!package)
        return fail(ctx, PICO_ERR_RUNTIME, "out of memory");

    pico_value v;

    v = pico_val_cstr(package_path.c_str());
    pico_object_set(package, "Path", v);
    pico_val_release(v);

    v = pico_val_cstr(version.c_str());
    pico_object_set(package, "Version", v);
    pico_val_release(v);

    v = pico_val_cstr(changelog.c_str());
    pico_object_set(package, "Changelog", v);
    pico_val_release(v);

    pico_value result = pico_val_take_object(package);

    // The .NET cmdlet assigns $Return as well as emitting, so a Package script
    // that ends in New-Package returns the package without a further step.
    if (ctx->interp)
        pico_scope_set(ctx->interp, "Return", result);

    const pico_status status = pico_emit(ctx, result);
    pico_val_release(result);

    return status;
}

const PicoParamDef new_package_params[] = {
    { "Path", 0, 0 }, { "Version", 0, 1 }, { "Changelog", 0, 2 }
};

const PicoCmdletDef new_package_def = {
    "New-Package", new_package_params, 3, new_package_begin, NULL, NULL,
    "Builds a package result and assigns it to $Return."
};

} // namespace

const PicoCmdletDef* cmdlet_get_game_manifest()   { return &get_game_manifest_def; }
const PicoCmdletDef* cmdlet_write_game_manifest() { return &write_game_manifest_def; }
const PicoCmdletDef* cmdlet_new_package()         { return &new_package_def; }

} // namespace cmdlets
} // namespace lancommander
