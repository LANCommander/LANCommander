// The cmdlets that talk to the server: Get-UserCustomField,
// Update-UserCustomField, Out-PlayerAvatar and Expand-LatestArchive.
//
// These are the only ones needing anything beyond their parameters, which they
// take from cmdlets::context(). When it has no HTTP client they fail with a
// message saying so rather than behaving as though the call succeeded.

#include "script/cmdlets/cmdlet_defs.h"

#include "lancommander/archive/zip_archive_extractor.h"
#include "lancommander/clients/game_client.h"
#include "lancommander/clients/profile_client.h"
#include "lancommander/clients/redistributable_client.h"
#include "lancommander/clients/tool_client.h"
#include "lancommander/script/cmdlets.h"
#include "lancommander/util/path.h"

#include <cstdio>
#include <string>
#include <vector>

namespace lancommander {
namespace cmdlets {

namespace {

const char* kNoHttp =
    "no server connection is configured for cmdlets; the host must call "
    "lancommander::cmdlets::set_context() with an IHttpClient";

// --- Get-UserCustomField --------------------------------------------------

pico_status get_user_custom_field_begin(PicoStage* ctx)
{
    std::string name;
    if (!arg_string(ctx, "Name", 0, &name) || name.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Name");

    if (!context().http)
        return fail(ctx, PICO_ERR_RUNTIME, kNoHttp);

    ProfileClient profile(*context().http);
    Result<std::string> value = profile.get_custom_field(name);

    if (!value)
        return fail(ctx, PICO_ERR_RUNTIME, value.error);

    return emit_string(ctx, value.value);
}

const PicoParamDef get_user_custom_field_params[] = {
    { "Name", 0, 0 }
};

const PicoCmdletDef get_user_custom_field_def = {
    "Get-UserCustomField", get_user_custom_field_params, 1,
    get_user_custom_field_begin, NULL, NULL,
    "Reads a custom field from the signed-in user's profile."
};

// --- Update-UserCustomField -----------------------------------------------

pico_status update_user_custom_field_begin(PicoStage* ctx)
{
    std::string name;
    std::string value;

    if (!arg_string(ctx, "Name", 0, &name) || name.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Name");
    if (!arg_string(ctx, "Value", 1, &value))
        return fail(ctx, PICO_ERR_ARG, "requires a -Value");

    if (!context().http)
        return fail(ctx, PICO_ERR_RUNTIME, kNoHttp);

    ProfileClient profile(*context().http);
    Result<std::string> updated = profile.update_custom_field(name, value);

    if (!updated)
        return fail(ctx, PICO_ERR_RUNTIME, updated.error);

    return emit_string(ctx, updated.value);
}

const PicoParamDef update_user_custom_field_params[] = {
    { "Name", 0, 0 }, { "Value", 0, 1 }
};

const PicoCmdletDef update_user_custom_field_def = {
    "Update-UserCustomField", update_user_custom_field_params, 2,
    update_user_custom_field_begin, NULL, NULL,
    "Sets a custom field on the signed-in user's profile."
};

// --- Out-PlayerAvatar -----------------------------------------------------

pico_status out_player_avatar_begin(PicoStage* ctx)
{
    if (!context().http)
        return fail(ctx, PICO_ERR_RUNTIME, kNoHttp);

    ProfileClient profile(*context().http);
    Result<std::vector<unsigned char> > avatar = profile.get_avatar();

    if (!avatar)
        return fail(ctx, PICO_ERR_RUNTIME, avatar.error);

    // One byte array, not one item per byte — the .NET cmdlet passes
    // enumerate: false to WriteObject for exactly this reason.
    return emit_bytes(ctx, avatar.value);
}

const PicoCmdletDef out_player_avatar_def = {
    "Out-PlayerAvatar", NULL, 0, out_player_avatar_begin, NULL, NULL,
    "Emits the signed-in user's avatar as a byte array."
};

// --- Expand-LatestArchive -------------------------------------------------

// Reads a variable out of the running interpreter as a string.
bool session_string(PicoStage* ctx, const char* name, std::string* out)
{
    if (!ctx->interp)
        return false;

    pico_value v;
    if (!pico_scope_get(ctx->interp, name, &v))
        return false;

    bool ok = false;
    if (v.type != PICO_VT_NULL) {
        PicoStr* s = pico_val_to_str(v);
        if (s) {
            *out = pico_str_cstr(s);
            pico_str_release(s);
            ok = !out->empty();
        }
    }

    pico_val_release(v);
    return ok;
}

// Reads `$<name>.Id` — the shape of $GameManifest, $Game, $Redistributable
// and $Tool.
bool session_object_id(PicoStage* ctx, const char* name, std::string* out)
{
    if (!ctx->interp)
        return false;

    pico_value v;
    if (!pico_scope_get(ctx->interp, name, &v))
        return false;

    bool ok = false;

    if (v.type == PICO_VT_OBJECT && v.u.o) {
        pico_value id;
        if (pico_object_get(v.u.o, "Id", &id) ||
            pico_object_get(v.u.o, "id", &id)) {
            PicoStr* s = pico_val_to_str(id);
            if (s) {
                *out = pico_str_cstr(s);
                pico_str_release(s);
                ok = !out->empty();
            }
            pico_val_release(id);
        }
    }

    pico_val_release(v);
    return ok;
}

enum EntityKind { EntityGame, EntityRedistributable, EntityTool, EntityNone };

// Explicit parameters win; then the more specific context variables, then the
// game ones. Same precedence as the .NET cmdlet's ResolveDownloadTarget.
EntityKind resolve_target(PicoStage* ctx, std::string* id)
{
    if (arg_string(ctx, "RedistributableId", -1, id) && !id->empty())
        return EntityRedistributable;
    if (arg_string(ctx, "ToolId", -1, id) && !id->empty())
        return EntityTool;
    if (arg_string(ctx, "GameId", -1, id) && !id->empty())
        return EntityGame;

    if (session_object_id(ctx, "Redistributable", id))
        return EntityRedistributable;
    if (session_object_id(ctx, "Tool", id))
        return EntityTool;
    if (session_object_id(ctx, "GameManifest", id))
        return EntityGame;
    if (session_object_id(ctx, "Game", id))
        return EntityGame;

    return EntityNone;
}

pico_status extract_to(PicoStage* ctx, const std::string& archive_path,
                       const std::string& destination)
{
    ZipArchiveExtractor fallback;
    IArchiveExtractor& extractor =
        context().extractor ? *context().extractor
                            : static_cast<IArchiveExtractor&>(fallback);

    if (!context().extractor && !ZipArchiveExtractor::available()) {
        return fail(ctx, PICO_ERR_RUNTIME,
                    "this build has no zip backend; run setup-vendor.ps1 to "
                    "fetch picoposh's zlib and libzip submodules");
    }

    // Overwrite rather than skip: the .NET cmdlet passes Overwrite = true, and
    // a script calling this wants the archive's contents to win.
    ExtractionResult result = extractor.extract(archive_path, destination, false);

    if (result.canceled)
        return fail(ctx, PICO_ERR_RUNTIME, "extraction was canceled");
    if (!result.success)
        return fail(ctx, PICO_ERR_IO, result.error);

    return emit_string(ctx, destination);
}

pico_status expand_latest_archive_begin(PicoStage* ctx)
{
    // picoposh has no parameter aliases, so the .NET cmdlet's -Destination and
    // -OutputPath are declared as their own parameters and checked here.
    std::string destination;
    if ((!arg_string(ctx, "DestinationPath", 0, &destination) || destination.empty()) &&
        (!arg_string(ctx, "Destination", -1, &destination) || destination.empty()) &&
        (!arg_string(ctx, "OutputPath", -1, &destination) || destination.empty())) {
        // The .NET cmdlet defaults to the session's current location, which
        // here is the process working directory the runner set.
        destination = path::get_current_directory();
    }

    Result<bool> made = path::create_directories(destination);
    if (!made)
        return fail(ctx, PICO_ERR_IO, made.error);

    // A server-side packaging script has the archive on disk already; only
    // fall through to downloading when it does not.
    std::string local_archive;
    if (session_string(ctx, "LatestArchivePath", &local_archive) &&
        path::exists(local_archive)) {
        return extract_to(ctx, local_archive, destination);
    }

    std::string id;
    const EntityKind kind = resolve_target(ctx, &id);

    if (kind == EntityNone) {
        return fail(ctx, PICO_ERR_ARG,
                    "could not determine what to download; ensure a context "
                    "variable ($GameManifest, $Game, $Redistributable or $Tool) "
                    "is available, or pass -GameId, -RedistributableId or "
                    "-ToolId");
    }

    if (!context().http)
        return fail(ctx, PICO_ERR_RUNTIME, kNoHttp);

    // Download to a temporary file rather than streaming: the extractor works
    // from a path, and picoposh has no streaming pipe to hand it.
    Result<std::string> temp = path::create_temp_file("lc_archive_", ".zip");
    if (!temp)
        return fail(ctx, PICO_ERR_IO, temp.error);

    Result<bool> downloaded = Result<bool>::fail("unknown entity");

    switch (kind) {
        case EntityRedistributable: {
            RedistributableClient client(*context().http);
            downloaded = client.download(id, temp.value);
            break;
        }
        case EntityTool: {
            ToolClient client(*context().http);
            downloaded = client.download(id, temp.value);
            break;
        }
        case EntityGame:
        default: {
            GameClient client(*context().http);
            downloaded = client.download(id, temp.value);
            break;
        }
    }

    if (!downloaded) {
        path::remove_file(temp.value);
        return fail(ctx, PICO_ERR_IO, downloaded.error);
    }

    const pico_status status = extract_to(ctx, temp.value, destination);
    path::remove_file(temp.value);

    return status;
}

const PicoParamDef expand_latest_archive_params[] = {
    { "DestinationPath", 0, 0 },   { "Destination", 0, -1 },
    { "OutputPath", 0, -1 },       { "GameId", 0, -1 },
    { "RedistributableId", 0, -1 }, { "ToolId", 0, -1 }
};

const PicoCmdletDef expand_latest_archive_def = {
    "Expand-LatestArchive", expand_latest_archive_params, 6,
    expand_latest_archive_begin, NULL, NULL,
    "Extracts the latest archive for a game, redistributable or tool."
};

} // namespace

const PicoCmdletDef* cmdlet_get_user_custom_field()
{
    return &get_user_custom_field_def;
}

const PicoCmdletDef* cmdlet_update_user_custom_field()
{
    return &update_user_custom_field_def;
}

const PicoCmdletDef* cmdlet_out_player_avatar()
{
    return &out_player_avatar_def;
}

const PicoCmdletDef* cmdlet_expand_latest_archive()
{
    return &expand_latest_archive_def;
}

} // namespace cmdlets
} // namespace lancommander
