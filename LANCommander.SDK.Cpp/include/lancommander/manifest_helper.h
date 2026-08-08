#ifndef LANCOMMANDER_MANIFEST_HELPER_H
#define LANCOMMANDER_MANIFEST_HELPER_H

#include <string>

#include "models/game.h"
#include "types.h"

namespace lancommander {
namespace manifest {

// Reads and writes the `Manifest.yml` the .NET SDK's ManifestHelper produces,
// in the same location:
//
//   <install directory>/.lancommander/<entity id>/Manifest.yml
//
// The file is YAML with PascalCase keys. Everything else in this SDK speaks
// JSON, so these convert at the boundary (see src/yaml/) rather than
// duplicating the model parsers — which means a directory written by either
// launcher is readable by the other.

// "Manifest.yml"
const char* file_name();

// The manifest inside an entity's metadata directory.
std::string path(const std::string& install_directory,
                 const std::string& entity_id);

// The manifest directly inside a directory, matching ManifestHelper's
// single-argument Read overload.
std::string path(const std::string& install_directory);

bool exists(const std::string& install_directory,
            const std::string& entity_id);

// The manifest as JSON, which is what `$GameManifest` wants — it preserves
// every field, including those GameManifest does not model.
Result<std::string> read_json(const std::string& manifest_path);

Result<GameManifest> read(const std::string& install_directory,
                          const std::string& entity_id);

// Writes `json` (any JSON object) as YAML. Returns the path written, matching
// ManifestHelper.Write's return.
Result<std::string> write_json(const std::string& manifest_path,
                               const std::string& json);

Result<std::string> write(const GameManifest& manifest,
                          const std::string& install_directory,
                          const std::string& entity_id);

} // namespace manifest
} // namespace lancommander

#endif // LANCOMMANDER_MANIFEST_HELPER_H
