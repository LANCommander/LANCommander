#ifndef LANCOMMANDER_JSON_HELPERS_H
#define LANCOMMANDER_JSON_HELPERS_H

#include <string>
#include <vector>

#include "cJSON.h"

#include "lancommander/models/auth.h"
#include "lancommander/models/game.h"
#include "lancommander/models/library.h"
#include "lancommander/models/profile.h"
#include "lancommander/models/redistributable.h"
#include "lancommander/models/save.h"
#include "lancommander/models/script.h"
#include "lancommander/models/server.h"
#include "lancommander/models/archive.h"
#include "lancommander/models/tool.h"
#include "lancommander/models/collection.h"
#include "lancommander/models/company.h"
#include "lancommander/models/engine.h"
#include "lancommander/models/genre.h"
#include "lancommander/models/platform.h"
#include "lancommander/models/tag.h"
#include "lancommander/models/play_session.h"
#include "lancommander/models/custom_field.h"
#include "lancommander/models/external_id.h"
#include "lancommander/models/multiplayer_mode.h"
#include "lancommander/models/lobby.h"
#include "lancommander/models/page.h"
#include "lancommander/models/package.h"
#include "lancommander/models/depot.h"
#include "lancommander/models/update_info.h"
#include "lancommander/models/error_response.h"
#include "lancommander/models/server_detail.h"
#include "lancommander/models/media.h"

namespace lancommander {
namespace json {

// RAII wrapper for cJSON*
struct JsonDoc {
    cJSON* root;
    explicit JsonDoc(const std::string& text) : root(cJSON_Parse(text.c_str())) {}
    ~JsonDoc() { if (root) cJSON_Delete(root); }
    explicit operator bool() const { return root != nullptr; }
    JsonDoc(const JsonDoc&) = delete;
    JsonDoc& operator=(const JsonDoc&) = delete;
};

// Primitive accessors — try camelCase first, then PascalCase.
std::string get_string(cJSON* obj, const char* camel, const char* pascal);
int get_int(cJSON* obj, const char* camel, const char* pascal, int def = 0);
long long get_long(cJSON* obj, const char* camel, const char* pascal, long long def = 0);
bool get_bool(cJSON* obj, const char* camel, const char* pascal, bool def = false);
cJSON* get_child(cJSON* obj, const char* camel, const char* pascal);

// Convenience overloads for single-name lookup.
std::string get_string(cJSON* obj, const char* key);
int get_int(cJSON* obj, const char* key, int def = 0);
bool get_bool(cJSON* obj, const char* key, bool def = false);

// Collect a string array of "name"/"Name" from an array of objects.
std::vector<std::string> collect_names(cJSON* arr);

// Collect a string array from a JSON array of strings.
std::vector<std::string> collect_strings(cJSON* arr);

// JSON string escaping for building request bodies.
std::string escape(const std::string& in);

// Model parsers — existing
AuthToken parse_auth_token(cJSON* obj);
Game parse_game(cJSON* obj);
MediaRef parse_media_ref(cJSON* obj);
ManifestAction parse_manifest_action(cJSON* obj);
ManifestSavePath parse_manifest_save_path(cJSON* obj);
Redistributable parse_redistributable(cJSON* obj);
Script parse_script(cJSON* obj);
EntityReference parse_entity_reference(cJSON* obj);
User parse_user(cJSON* obj);
GameSave parse_game_save(cJSON* obj);
DiscoveredServer parse_discovered_server(cJSON* obj);

// Model parsers — new
Archive parse_archive(cJSON* obj);
Tool parse_tool(cJSON* obj);
Collection parse_collection(cJSON* obj);
Company parse_company(cJSON* obj);
Engine parse_engine(cJSON* obj);
Genre parse_genre(cJSON* obj);
Platform parse_platform(cJSON* obj);
Tag parse_tag(cJSON* obj);
PlaySession parse_play_session(cJSON* obj);
GameCustomField parse_custom_field(cJSON* obj);
GameExternalId parse_external_id(cJSON* obj);
MultiplayerMode parse_multiplayer_mode(cJSON* obj);
Lobby parse_lobby(cJSON* obj);
Page parse_page(cJSON* obj);
Package parse_package(cJSON* obj);
Media parse_media(cJSON* obj);
DepotGame parse_depot_game(cJSON* obj);
DepotResults parse_depot_results(cJSON* obj);
CheckForUpdateResponse parse_check_for_update_response(cJSON* obj);
ErrorResponse parse_error_response(cJSON* obj);
ErrorInfo parse_error_info(cJSON* obj);
ServerConsole parse_server_console(cJSON* obj);
ServerHttpPath parse_server_http_path(cJSON* obj);
ServerDetail parse_server_detail(cJSON* obj);

} // namespace json
} // namespace lancommander

#endif // LANCOMMANDER_JSON_HELPERS_H
