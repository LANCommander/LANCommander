#ifndef LANCOMMANDER_CMDLET_DEFS_H
#define LANCOMMANDER_CMDLET_DEFS_H

#include "script/cmdlets/cmdlet_support.h"

// Each cmdlet lives in a translation unit grouped by what it does, and exposes
// its definition through one of these. The registration table in cmdlets.cpp
// is the only place that lists them all, so adding a cmdlet means touching
// exactly two files.
//
// Every definition is static const with static storage duration, which is what
// pico_register_cmdlet requires — it borrows the definition rather than
// copying it.

namespace lancommander {
namespace cmdlets {

// cmdlets_system.cpp
const PicoCmdletDef* cmdlet_get_runtime();
const PicoCmdletDef* cmdlet_get_sanitized_path();
const PicoCmdletDef* cmdlet_get_primary_display();
void primary_display_size(long* width, long* height);

// cmdlets_math.cpp
const PicoCmdletDef* cmdlet_convert_aspect_ratio();
const PicoCmdletDef* cmdlet_get_horizontal_fov();
const PicoCmdletDef* cmdlet_get_vertical_fov();

// cmdlets_bytes.cpp
const PicoCmdletDef* cmdlet_convert_to_string_bytes();
const PicoCmdletDef* cmdlet_edit_patch_binary();

// cmdlets_text.cpp
const PicoCmdletDef* cmdlet_write_replace_content_in_file();
const PicoCmdletDef* cmdlet_update_ini_value();

// cmdlets_manifest.cpp
const PicoCmdletDef* cmdlet_get_game_manifest();
const PicoCmdletDef* cmdlet_write_game_manifest();
const PicoCmdletDef* cmdlet_new_package();

} // namespace cmdlets
} // namespace lancommander

#endif // LANCOMMANDER_CMDLET_DEFS_H
