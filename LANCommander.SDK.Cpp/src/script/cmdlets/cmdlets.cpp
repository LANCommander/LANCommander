#include "lancommander/script/cmdlets.h"

#include "script/cmdlets/cmdlet_defs.h"

namespace lancommander {
namespace cmdlets {

namespace {

Context g_context;

typedef const PicoCmdletDef* (*DefAccessor)();

// The one place every cmdlet is listed. Each accessor returns a pointer to a
// static const definition: pico_register_cmdlet borrows rather than copies, so
// the definition and everything it points at must outlive every script run.
const DefAccessor kAccessors[] = {
    cmdlet_convert_aspect_ratio,
    cmdlet_convert_to_string_bytes,
    cmdlet_edit_patch_binary,
    cmdlet_expand_latest_archive,
    cmdlet_get_game_manifest,
    cmdlet_get_horizontal_fov,
    cmdlet_get_primary_display,
    cmdlet_get_runtime,
    cmdlet_get_sanitized_path,
    cmdlet_get_user_custom_field,
    cmdlet_get_vertical_fov,
    cmdlet_new_package,
    cmdlet_out_player_avatar,
    cmdlet_update_ini_value,
    cmdlet_update_user_custom_field,
    cmdlet_write_game_manifest,
    cmdlet_write_replace_content_in_file
};

const int kCount = (int)(sizeof(kAccessors) / sizeof(kAccessors[0]));

} // namespace

void set_context(const Context& context) { g_context = context; }

const Context& context() { return g_context; }

void clear_context() { g_context = Context(); }

Result<bool> register_all()
{
    for (int i = 0; i < kCount; ++i) {
        const PicoCmdletDef* def = kAccessors[i]();
        if (!def)
            continue;

        if (pico_register_cmdlet(def) != 0) {
            return Result<bool>::fail(
                std::string("could not register the cmdlet ") +
                (def->name ? def->name : "<unnamed>"));
        }
    }

    return Result<bool>::ok(true);
}

void unregister_all()
{
    pico_unregister_cmdlets();
}

std::vector<std::string> names()
{
    std::vector<std::string> out;
    out.reserve((std::size_t)kCount);

    for (int i = 0; i < kCount; ++i) {
        const PicoCmdletDef* def = kAccessors[i]();
        if (def && def->name)
            out.push_back(def->name);
    }

    return out;
}

} // namespace cmdlets
} // namespace lancommander
