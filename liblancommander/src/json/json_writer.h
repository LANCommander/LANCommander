#ifndef LANCOMMANDER_JSON_WRITER_H
#define LANCOMMANDER_JSON_WRITER_H

#include <string>
#include <vector>

#include "lancommander/models/custom_field.h"
#include "lancommander/models/game.h"
#include "lancommander/models/redistributable.h"
#include "lancommander/models/script.h"
#include "lancommander/models/tool.h"

namespace lancommander {
namespace json {

// Compact single-line JSON, built on the vendored cJSON printer so escaping is
// correct for free. Keys are PascalCase to match the server's serialization;
// the parse_* helpers accept either case, so these round-trip.
//
// Prefer a raw server response body where one is available (see
// GameClient::get_manifest_json) — these serializers can only emit the fields
// the C++ structs model, which is a subset of what the server sends.
std::string serialize_game_manifest(const GameManifest& manifest);
std::string serialize_redistributable(const Redistributable& redistributable);
std::string serialize_tool(const Tool& tool);
std::string serialize_script(const Script& script);
std::string serialize_custom_fields(const std::vector<GameCustomField>& fields);

} // namespace json
} // namespace lancommander

#endif // LANCOMMANDER_JSON_WRITER_H
