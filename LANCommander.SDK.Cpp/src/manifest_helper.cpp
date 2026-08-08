#include "lancommander/manifest_helper.h"

#include "lancommander/clients/game_client.h"
#include "lancommander/script/script_helper.h"
#include "lancommander/util/path.h"

#include "json/json_writer.h"
#include "yaml/yaml_convert.h"

#include <cstdio>

namespace lancommander {
namespace manifest {

namespace {

const char* kManifestFileName = "Manifest.yml";

Result<std::string> read_file(const std::string& path)
{
    std::FILE* file = std::fopen(path.c_str(), "rb");
    if (!file)
        return Result<std::string>::fail("could not open manifest: " + path);

    std::string contents;
    char buffer[4096];
    std::size_t read = 0;
    while ((read = std::fread(buffer, 1, sizeof(buffer), file)) > 0)
        contents.append(buffer, read);
    std::fclose(file);

    return Result<std::string>::ok(contents);
}

Result<bool> write_file(const std::string& path, const std::string& contents)
{
    Result<bool> dir = path::create_directories(path::parent(path));
    if (!dir)
        return dir;

    std::FILE* file = std::fopen(path.c_str(), "wb");
    if (!file)
        return Result<bool>::fail("could not write manifest: " + path);

    const std::size_t written =
        contents.empty() ? 0 : std::fwrite(contents.data(), 1, contents.size(), file);
    std::fclose(file);

    if (written != contents.size())
        return Result<bool>::fail("short write for manifest: " + path);

    return Result<bool>::ok(true);
}

} // namespace

const char* file_name() { return kManifestFileName; }

std::string path(const std::string& install_directory,
                 const std::string& entity_id)
{
    return path::combine(
        script::metadata_directory_path(install_directory, entity_id),
        kManifestFileName);
}

std::string path(const std::string& install_directory)
{
    return path::combine(install_directory, kManifestFileName);
}

bool exists(const std::string& install_directory, const std::string& entity_id)
{
    return path::exists(path(install_directory, entity_id));
}

Result<std::string> read_json(const std::string& manifest_path)
{
    Result<std::string> text = read_file(manifest_path);
    if (!text)
        return text;

    Result<std::string> json = yaml::to_json(text.value);
    if (!json)
        return Result<std::string>::fail(manifest_path + ": " + json.error);

    return json;
}

Result<GameManifest> read(const std::string& install_directory,
                          const std::string& entity_id)
{
    Result<std::string> json = read_json(path(install_directory, entity_id));
    if (!json)
        return Result<GameManifest>::fail(json.error);

    GameManifest parsed;
    std::string error;
    if (!parse_manifest_json(json.value, &parsed, &error))
        return Result<GameManifest>::fail(error);

    return Result<GameManifest>::ok(parsed);
}

Result<std::string> write_json(const std::string& manifest_path,
                               const std::string& json)
{
    Result<std::string> text = yaml::from_json(json);
    if (!text)
        return text;

    Result<bool> written = write_file(manifest_path, text.value);
    if (!written)
        return Result<std::string>::fail(written.error);

    return Result<std::string>::ok(manifest_path);
}

Result<std::string> write(const GameManifest& game_manifest,
                          const std::string& install_directory,
                          const std::string& entity_id)
{
    return write_json(path(install_directory, entity_id),
                      json::serialize_game_manifest(game_manifest));
}

} // namespace manifest
} // namespace lancommander
