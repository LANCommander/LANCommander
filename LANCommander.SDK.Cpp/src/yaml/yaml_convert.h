#ifndef LANCOMMANDER_YAML_CONVERT_H
#define LANCOMMANDER_YAML_CONVERT_H

#include <string>

#include "lancommander/types.h"

namespace lancommander {
namespace yaml {

// YAML <-> JSON conversion, built on libyaml.
//
// Everything else in the SDK speaks JSON, so rather than a second set of model
// parsers this converts at the boundary: a Manifest.yml written by the .NET
// launcher becomes JSON and goes through the same `parse_manifest_json` as an
// API response.
//
// Scope matches what YamlDotNet emits for LANCommander's models: block and
// flow collections, plain and quoted scalars, comments, multiple documents
// (only the first is read). Anchors and aliases are rejected rather than
// silently mishandled — nothing in LANCommander emits them.
//
// Plain scalars are typed by inspection: `true`/`false`, `null`/`~`/empty,
// integers and floats become their JSON equivalents; anything else, and
// anything quoted, stays a string.
Result<std::string> to_json(const std::string& yaml_text);

// The inverse. Strings that could be misread as a number, a boolean or null on
// the way back are emitted quoted.
Result<std::string> from_json(const std::string& json_text);

} // namespace yaml
} // namespace lancommander

#endif // LANCOMMANDER_YAML_CONVERT_H
