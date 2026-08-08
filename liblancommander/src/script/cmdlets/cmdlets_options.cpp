// Get-GameOptions and Get-RedistributableOptions.
//
// A game or redistributable can carry an OptionSchema: a YAML document
// describing its configurable options, nested into groups, with defaults. The
// per-entity `Options` map then overrides those defaults using dot-notation
// keys ("Proton.PROTONPATH"). These cmdlets resolve the two into the nested
// object a script actually wants.
//
// The .NET versions deserialise into typed OptionSchema/OptionDefinition
// models. Here the schema stays a JSON tree — yaml::to_json gets it there, and
// emit_json takes the result back out — because these two cmdlets only read a
// handful of its fields (Type, Default, Options, ItemType, Fields). Porting
// the models would add a layer that nothing else in the SDK needs.

#include "script/cmdlets/cmdlet_defs.h"

#include "lancommander/manifest_helper.h"
#include "lancommander/util/path.h"

#include "cJSON.h"
#include "json/json_helpers.h"
#include "yaml/yaml_convert.h"

#include <cctype>
#include <cstdlib>
#include <string>
#include <utility>
#include <vector>

namespace lancommander {
namespace cmdlets {

namespace {

// Resolved options in schema order: .NET builds these in a Dictionary, whose
// enumeration follows insertion, so an override of an existing key keeps its
// original position and a new one is appended. A vector reproduces that
// without depending on a dictionary quirk.
typedef std::vector<std::pair<std::string, std::string> > ResolvedOptions;

struct FlatOption {
    std::string key;      // dot-notation path
    cJSON* definition;    // borrowed from the schema tree
};

bool equals_ignore_case(const std::string& a, const char* b)
{
    std::size_t i = 0;
    for (; i < a.size() && b[i] != '\0'; ++i) {
        if (std::tolower((unsigned char)a[i]) != std::tolower((unsigned char)b[i]))
            return false;
    }
    return i == a.size() && b[i] == '\0';
}

bool is_blank(const std::string& text)
{
    for (std::size_t i = 0; i < text.size(); ++i) {
        if (!std::isspace((unsigned char)text[i]))
            return false;
    }
    return true;
}

cJSON* child(cJSON* node, const char* pascal, const char* camel)
{
    return node ? json::get_child(node, camel, pascal) : NULL;
}

bool is_list(cJSON* definition)
{
    return equals_ignore_case(json::get_string(definition, "type", "Type"), "list");
}

bool is_composite_list(cJSON* definition)
{
    if (!is_list(definition))
        return false;

    cJSON* fields = child(definition, "Fields", "fields");
    return fields && (fields->type & 0xFF) == cJSON_Object && fields->child != NULL;
}

std::string print_json(cJSON* node)
{
    if (!node)
        return std::string();

    char* text = cJSON_PrintUnformatted(node);
    const std::string out = text ? std::string(text) : std::string();

    if (text)
        cJSON_free(text);

    return out;
}

// The canonical string form used by per-entity option storage: the raw value
// for a scalar, a JSON array for a list.
std::string default_as_string(cJSON* definition)
{
    cJSON* value = child(definition, "Default", "default");
    if (!value || (value->type & 0xFF) == cJSON_NULL)
        return std::string();

    if (is_list(definition))
        return print_json(value);

    return json::get_string(definition, "default", "Default");
}

// Leaves are options carrying a Type; group nodes only hold children. A list's
// Fields describe per-item shape, not sibling options, so lists are leaves too.
void flatten(cJSON* options, const std::string& prefix,
             std::vector<FlatOption>* out)
{
    if (!options || (options->type & 0xFF) != cJSON_Object)
        return;

    for (cJSON* entry = options->child; entry; entry = entry->next) {
        const std::string name = entry->string ? entry->string : "";
        const std::string key = prefix.empty() ? name : prefix + "." + name;

        if (!json::get_string(entry, "type", "Type").empty()) {
            FlatOption flat;
            flat.key = key;
            flat.definition = entry;
            out->push_back(flat);
        }

        if (!is_list(entry))
            flatten(child(entry, "Options", "options"), key, out);
    }
}

const FlatOption* find_flat(const std::vector<FlatOption>& flat,
                            const std::string& key)
{
    for (std::size_t i = 0; i < flat.size(); ++i) {
        if (flat[i].key == key)
            return &flat[i];
    }
    return NULL;
}

void set_resolved(ResolvedOptions* resolved, const std::string& key,
                  const std::string& value)
{
    for (std::size_t i = 0; i < resolved->size(); ++i) {
        if ((*resolved)[i].first == key) {
            (*resolved)[i].second = value;
            return;
        }
    }
    resolved->push_back(std::make_pair(key, value));
}

// A JSON element as the string .NET's ReadJsonScalar would produce: strings
// unwrapped, null dropped, everything else as its raw text.
bool read_scalar(cJSON* element, std::string* out)
{
    if (!element)
        return false;

    switch (element->type & 0xFF) {
        case cJSON_String:
            *out = element->valuestring ? element->valuestring : "";
            return true;
        case cJSON_NULL:
            return false;
        default:
            *out = print_json(element);
            return true;
    }
}

cJSON* coerce_scalar(const std::string& value, const std::string& type)
{
    if (equals_ignore_case(type, "int")) {
        // A value that does not parse falls back to the string, rather than
        // silently becoming zero.
        const char* start = value.c_str();
        char* end = NULL;
        const long parsed = std::strtol(start, &end, 10);

        if (end != start && *end == '\0')
            return cJSON_CreateNumber((double)parsed);

        return cJSON_CreateString(value.c_str());
    }

    if (equals_ignore_case(type, "bool")) {
        if (equals_ignore_case(value, "true"))
            return cJSON_CreateTrue();
        if (equals_ignore_case(value, "false"))
            return cJSON_CreateFalse();
        return cJSON_CreateString(value.c_str());
    }

    return cJSON_CreateString(value.c_str());
}

// Turns a list option's stored JSON string into a real array. Composite lists
// become an array of objects shaped by Fields; scalar lists become an array of
// values coerced to ItemType.
cJSON* hydrate_list(cJSON* definition, const std::string& stored)
{
    const bool composite = is_composite_list(definition);

    if (is_blank(stored))
        return cJSON_CreateArray();

    cJSON* parsed = cJSON_Parse(stored.c_str());
    if (!parsed) {
        // Malformed: surface the raw string rather than throwing inside a
        // script, matching the .NET behaviour.
        return cJSON_CreateString(stored.c_str());
    }

    if ((parsed->type & 0xFF) != cJSON_Array) {
        cJSON_Delete(parsed);
        return cJSON_CreateString(stored.c_str());
    }

    cJSON* result = cJSON_CreateArray();
    if (!result) {
        cJSON_Delete(parsed);
        return NULL;
    }

    if (composite) {
        cJSON* fields = child(definition, "Fields", "fields");

        for (cJSON* item = parsed->child; item; item = item->next) {
            cJSON* row = cJSON_CreateObject();
            if (!row)
                continue;

            for (cJSON* field = fields->child; field; field = field->next) {
                const std::string field_name = field->string ? field->string : "";
                const std::string field_type =
                    json::get_string(field, "type", "Type");

                std::string value;
                bool have = false;

                if ((item->type & 0xFF) == cJSON_Object) {
                    cJSON* supplied = cJSON_GetObjectItem(item, field_name.c_str());
                    have = read_scalar(supplied, &value);
                }

                if (!have) {
                    // Fall back to the field's own default.
                    const std::string fallback =
                        json::get_string(field, "default", "Default");
                    if (!is_blank(fallback)) {
                        value = fallback;
                        have = true;
                    }
                }

                cJSON_AddItemToObject(row, field_name.c_str(),
                                      have ? coerce_scalar(value, field_type)
                                           : cJSON_CreateNull());
            }

            cJSON_AddItemToArray(result, row);
        }
    } else {
        std::string item_type = json::get_string(definition, "itemType", "ItemType");
        if (is_blank(item_type))
            item_type = "string";

        for (cJSON* item = parsed->child; item; item = item->next) {
            std::string value;
            if (read_scalar(item, &value))
                cJSON_AddItemToArray(result, coerce_scalar(value, item_type));
            else
                cJSON_AddItemToArray(result, cJSON_CreateNull());
        }
    }

    cJSON_Delete(parsed);

    return result;
}

// Expands a dot-notation key into nested objects, creating them as needed. An
// intermediate that already holds a non-object is replaced, as in .NET.
void set_nested(cJSON* root, const std::string& dot_path, cJSON* value)
{
    std::vector<std::string> parts;
    std::size_t start = 0;

    for (;;) {
        const std::size_t dot = dot_path.find('.', start);
        if (dot == std::string::npos) {
            parts.push_back(dot_path.substr(start));
            break;
        }
        parts.push_back(dot_path.substr(start, dot - start));
        start = dot + 1;
    }

    cJSON* current = root;

    for (std::size_t i = 0; i + 1 < parts.size(); ++i) {
        cJSON* existing = cJSON_GetObjectItem(current, parts[i].c_str());

        if (existing && (existing->type & 0xFF) == cJSON_Object) {
            current = existing;
            continue;
        }

        cJSON* group = cJSON_CreateObject();
        if (!group)
            return;

        if (existing)
            cJSON_ReplaceItemInObject(current, parts[i].c_str(), group);
        else
            cJSON_AddItemToObject(current, parts[i].c_str(), group);

        current = group;
    }

    const std::string& leaf = parts[parts.size() - 1];

    if (cJSON_GetObjectItem(current, leaf.c_str()))
        cJSON_ReplaceItemInObject(current, leaf.c_str(), value);
    else
        cJSON_AddItemToObject(current, leaf.c_str(), value);
}

// The shared core: schema YAML plus an overrides map, resolved into the nested
// JSON object both cmdlets emit.
pico_status emit_resolved_options(PicoStage* ctx, const std::string& schema_yaml,
                                  cJSON* overrides)
{
    if (is_blank(schema_yaml))
        return emit_json(ctx, "{}");

    Result<std::string> schema_json = yaml::to_json(schema_yaml);
    if (!schema_json)
        return fail(ctx, PICO_ERR_RUNTIME,
                    "could not parse the option schema: " + schema_json.error);

    cJSON* schema = cJSON_Parse(schema_json.value.c_str());
    if (!schema)
        return fail(ctx, PICO_ERR_RUNTIME, "could not read the option schema");

    std::vector<FlatOption> flat;
    flatten(child(schema, "Options", "options"), std::string(), &flat);

    ResolvedOptions resolved;

    for (std::size_t i = 0; i < flat.size(); ++i) {
        const std::string value = default_as_string(flat[i].definition);
        if (!is_blank(value))
            set_resolved(&resolved, flat[i].key, value);
    }

    if (overrides && (overrides->type & 0xFF) == cJSON_Object) {
        for (cJSON* entry = overrides->child; entry; entry = entry->next) {
            const std::string key = entry->string ? entry->string : "";
            set_resolved(&resolved, key,
                         json::get_string(overrides, key.c_str()));
        }
    }

    cJSON* result = cJSON_CreateObject();
    if (!result) {
        cJSON_Delete(schema);
        return fail(ctx, PICO_ERR_RUNTIME, "out of memory");
    }

    for (std::size_t i = 0; i < resolved.size(); ++i) {
        const FlatOption* definition = find_flat(flat, resolved[i].first);

        cJSON* value = NULL;
        if (definition && is_list(definition->definition))
            value = hydrate_list(definition->definition, resolved[i].second);
        else
            value = cJSON_CreateString(resolved[i].second.c_str());

        if (value)
            set_nested(result, resolved[i].first, value);
    }

    const std::string out = print_json(result);

    cJSON_Delete(result);
    cJSON_Delete(schema);

    return emit_json(ctx, out);
}

// Reads the manifest for -Path / -Id as a JSON tree. Returns NULL having
// already failed the stage.
cJSON* read_manifest(PicoStage* ctx, std::string* directory_out,
                     std::string* id_out)
{
    std::string directory;
    std::string id;

    if (!arg_string(ctx, "Path", 0, &directory)) {
        fail(ctx, PICO_ERR_ARG, "requires a -Path");
        return NULL;
    }
    if (!arg_string(ctx, "Id", -1, &id)) {
        fail(ctx, PICO_ERR_ARG, "requires an -Id");
        return NULL;
    }

    const std::string manifest_path = manifest::path(directory, id);
    if (!path::exists(manifest_path)) {
        fail(ctx, PICO_ERR_IO, "could not read the game manifest at " + manifest_path);
        return NULL;
    }

    Result<std::string> json = manifest::read_json(manifest_path);
    if (!json) {
        fail(ctx, PICO_ERR_IO, json.error);
        return NULL;
    }

    cJSON* manifest = cJSON_Parse(json.value.c_str());
    if (!manifest) {
        fail(ctx, PICO_ERR_RUNTIME, "could not read the game manifest");
        return NULL;
    }

    if (directory_out) *directory_out = directory;
    if (id_out) *id_out = id;

    return manifest;
}

// --- Get-GameOptions ------------------------------------------------------

pico_status get_game_options_begin(PicoStage* ctx)
{
    cJSON* manifest = read_manifest(ctx, NULL, NULL);
    if (!manifest)
        return PICO_ERR_RUNTIME;

    const std::string schema =
        json::get_string(manifest, "optionSchema", "OptionSchema");

    const pico_status status = emit_resolved_options(
        ctx, schema, child(manifest, "Options", "options"));

    cJSON_Delete(manifest);

    return status;
}

const PicoParamDef get_game_options_params[] = {
    { "Path", 0, 0 }, { "Id", 0, -1 }
};

const PicoCmdletDef get_game_options_def = {
    "Get-GameOptions", get_game_options_params, 2,
    get_game_options_begin, NULL, NULL,
    "Resolves a game's option schema and overrides into a nested object."
};

// --- Get-RedistributableOptions -------------------------------------------

pico_status get_redistributable_options_begin(PicoStage* ctx)
{
    std::string name;
    if (!arg_string(ctx, "Name", -1, &name) || name.empty())
        return fail(ctx, PICO_ERR_ARG, "requires a -Name");

    cJSON* manifest = read_manifest(ctx, NULL, NULL);
    if (!manifest)
        return PICO_ERR_RUNTIME;

    cJSON* redistributables = child(manifest, "Redistributables", "redistributables");
    cJSON* target = NULL;

    if (redistributables && (redistributables->type & 0xFF) == cJSON_Array) {
        for (cJSON* entry = redistributables->child; entry; entry = entry->next) {
            if (equals_ignore_case(json::get_string(entry, "name", "Name"),
                                   name.c_str())) {
                target = entry;
                break;
            }
        }
    }

    if (!target) {
        cJSON_Delete(manifest);
        return fail(ctx, PICO_ERR_ARG,
                    "redistributable '" + name + "' not found in manifest");
    }

    const std::string schema =
        json::get_string(target, "optionSchema", "OptionSchema");

    const pico_status status = emit_resolved_options(
        ctx, schema, child(target, "Options", "options"));

    cJSON_Delete(manifest);

    return status;
}

const PicoParamDef get_redistributable_options_params[] = {
    { "Path", 0, 0 }, { "Id", 0, -1 }, { "Name", 0, -1 }
};

const PicoCmdletDef get_redistributable_options_def = {
    "Get-RedistributableOptions", get_redistributable_options_params, 3,
    get_redistributable_options_begin, NULL, NULL,
    "Resolves a redistributable's option schema and overrides into a nested object."
};

} // namespace

const PicoCmdletDef* cmdlet_get_game_options()
{
    return &get_game_options_def;
}

const PicoCmdletDef* cmdlet_get_redistributable_options()
{
    return &get_redistributable_options_def;
}

} // namespace cmdlets
} // namespace lancommander
