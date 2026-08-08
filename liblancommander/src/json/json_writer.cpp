#include "json/json_writer.h"

#include "cJSON.h"

namespace lancommander {
namespace json {

namespace {

// cJSON_PrintUnformatted hands back a buffer owned by cJSON's allocator. The
// SDK is exception-free, so a plain destructor is all the safety needed.
struct PrintedJson {
    char* text;

    explicit PrintedJson(cJSON* node)
        : text(node ? cJSON_PrintUnformatted(node) : NULL) {}

    ~PrintedJson() { if (text) cJSON_free(text); }

    std::string str() const { return text ? std::string(text) : std::string(); }

private:
    PrintedJson(const PrintedJson&);
    PrintedJson& operator=(const PrintedJson&);
};

// Owns a cJSON tree for the duration of a serialize_* call.
struct JsonNode {
    cJSON* node;

    explicit JsonNode(cJSON* n) : node(n) {}
    ~JsonNode() { if (node) cJSON_Delete(node); }

    std::string print() const { return PrintedJson(node).str(); }

private:
    JsonNode(const JsonNode&);
    JsonNode& operator=(const JsonNode&);
};

void add_string(cJSON* obj, const char* key, const std::string& value)
{
    cJSON_AddStringToObject(obj, key, value.c_str());
}

cJSON* build_script(const Script& s)
{
    cJSON* obj = cJSON_CreateObject();
    if (!obj)
        return NULL;

    cJSON_AddNumberToObject(obj, "Type", static_cast<double>(s.type));
    add_string(obj, "Name", s.name);
    add_string(obj, "Description", s.description);
    add_string(obj, "Contents", s.contents);
    cJSON_AddBoolToObject(obj, "RequiresAdmin", s.requires_admin ? 1 : 0);
    cJSON_AddNumberToObject(obj, "Platforms", static_cast<double>(s.platforms));

    return obj;
}

cJSON* build_custom_field(const GameCustomField& f)
{
    cJSON* obj = cJSON_CreateObject();
    if (!obj)
        return NULL;

    add_string(obj, "Name", f.name);
    add_string(obj, "Value", f.value);

    return obj;
}

cJSON* build_scripts_array(const std::vector<Script>& scripts)
{
    cJSON* arr = cJSON_CreateArray();
    if (!arr)
        return NULL;

    for (std::size_t i = 0; i < scripts.size(); ++i) {
        cJSON* item = build_script(scripts[i]);
        if (item)
            cJSON_AddItemToArray(arr, item);
    }

    return arr;
}

cJSON* build_manifest_action(const ManifestAction& a)
{
    cJSON* obj = cJSON_CreateObject();
    if (!obj)
        return NULL;

    add_string(obj, "Name", a.name);
    add_string(obj, "Path", a.path);
    add_string(obj, "Arguments", a.arguments);
    add_string(obj, "WorkingDirectory", a.working_directory);
    cJSON_AddBoolToObject(obj, "IsPrimaryAction", a.is_primary ? 1 : 0);
    cJSON_AddNumberToObject(obj, "SortOrder", a.sort_order);

    cJSON* vars = cJSON_CreateObject();
    if (vars) {
        for (std::map<std::string, std::string>::const_iterator it = a.variables.begin();
             it != a.variables.end(); ++it) {
            cJSON_AddStringToObject(vars, it->first.c_str(), it->second.c_str());
        }
        cJSON_AddItemToObject(obj, "Variables", vars);
    }

    return obj;
}

cJSON* build_manifest_save_path(const ManifestSavePath& s)
{
    cJSON* obj = cJSON_CreateObject();
    if (!obj)
        return NULL;

    add_string(obj, "Id", s.id);
    add_string(obj, "Path", s.path);
    add_string(obj, "WorkingDirectory", s.working_directory);
    cJSON_AddBoolToObject(obj, "IsFile", s.is_file ? 1 : 0);
    cJSON_AddBoolToObject(obj, "IsRegex", s.is_regex ? 1 : 0);

    return obj;
}

} // namespace

std::string serialize_game_manifest(const GameManifest& manifest)
{
    JsonNode root(cJSON_CreateObject());
    if (!root.node)
        return "{}";

    add_string(root.node, "Id", manifest.id);
    add_string(root.node, "Title", manifest.title);
    add_string(root.node, "Version", manifest.version);

    cJSON* actions = cJSON_CreateArray();
    if (actions) {
        for (std::size_t i = 0; i < manifest.actions.size(); ++i) {
            cJSON* item = build_manifest_action(manifest.actions[i]);
            if (item) cJSON_AddItemToArray(actions, item);
        }
        cJSON_AddItemToObject(root.node, "Actions", actions);
    }

    cJSON* save_paths = cJSON_CreateArray();
    if (save_paths) {
        for (std::size_t i = 0; i < manifest.save_paths.size(); ++i) {
            cJSON* item = build_manifest_save_path(manifest.save_paths[i]);
            if (item) cJSON_AddItemToArray(save_paths, item);
        }
        cJSON_AddItemToObject(root.node, "SavePaths", save_paths);
    }

    cJSON* redists = cJSON_CreateArray();
    if (redists) {
        for (std::size_t i = 0; i < manifest.redistributables.size(); ++i) {
            cJSON* item = cJSON_CreateObject();
            if (!item) continue;
            add_string(item, "Id", manifest.redistributables[i].id);
            add_string(item, "Name", manifest.redistributables[i].name);
            cJSON_AddItemToArray(redists, item);
        }
        cJSON_AddItemToObject(root.node, "Redistributables", redists);
    }

    cJSON* fields = cJSON_CreateArray();
    if (fields) {
        for (std::size_t i = 0; i < manifest.custom_fields.size(); ++i) {
            cJSON* item = build_custom_field(manifest.custom_fields[i]);
            if (item) cJSON_AddItemToArray(fields, item);
        }
        cJSON_AddItemToObject(root.node, "CustomFields", fields);
    }

    cJSON* scripts = build_scripts_array(manifest.scripts);
    if (scripts)
        cJSON_AddItemToObject(root.node, "Scripts", scripts);

    return root.print();
}

std::string serialize_redistributable(const Redistributable& redistributable)
{
    JsonNode root(cJSON_CreateObject());
    if (!root.node)
        return "{}";

    add_string(root.node, "Id", redistributable.id);
    add_string(root.node, "Name", redistributable.name);
    add_string(root.node, "Description", redistributable.description);

    cJSON* scripts = build_scripts_array(redistributable.scripts);
    if (scripts)
        cJSON_AddItemToObject(root.node, "Scripts", scripts);

    return root.print();
}

std::string serialize_tool(const Tool& tool)
{
    JsonNode root(cJSON_CreateObject());
    if (!root.node)
        return "{}";

    add_string(root.node, "Id", tool.id);
    add_string(root.node, "Name", tool.name);
    add_string(root.node, "Description", tool.description);
    add_string(root.node, "Notes", tool.notes);
    add_string(root.node, "ReleasedOn", tool.released_on);
    add_string(root.node, "CreatedOn", tool.created_on);
    add_string(root.node, "UpdatedOn", tool.updated_on);

    cJSON* scripts = build_scripts_array(tool.scripts);
    if (scripts)
        cJSON_AddItemToObject(root.node, "Scripts", scripts);

    return root.print();
}

std::string serialize_script(const Script& script)
{
    JsonNode root(build_script(script));
    if (!root.node)
        return "{}";
    return root.print();
}

std::string serialize_custom_fields(const std::vector<GameCustomField>& fields)
{
    JsonNode root(cJSON_CreateArray());
    if (!root.node)
        return "[]";

    for (std::size_t i = 0; i < fields.size(); ++i) {
        cJSON* item = build_custom_field(fields[i]);
        if (item)
            cJSON_AddItemToArray(root.node, item);
    }

    return root.print();
}

} // namespace json
} // namespace lancommander
