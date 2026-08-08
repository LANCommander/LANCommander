#include "yaml/yaml_convert.h"

#include "cJSON.h"
#include "yaml.h"

#include <cctype>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

namespace lancommander {
namespace yaml {

namespace {

// --- shared scalar typing -------------------------------------------------

bool equals_ignore_case(const std::string& a, const char* b)
{
    std::size_t i = 0;
    for (; i < a.size() && b[i] != '\0'; ++i) {
        if (std::tolower(static_cast<unsigned char>(a[i])) !=
            std::tolower(static_cast<unsigned char>(b[i])))
            return false;
    }
    return i == a.size() && b[i] == '\0';
}

bool looks_like_number(const std::string& text, double* out)
{
    if (text.empty())
        return false;

    const char* start = text.c_str();
    char* end = NULL;
    const double value = std::strtod(start, &end);

    if (end == start)
        return false;
    while (*end != '\0' && std::isspace(static_cast<unsigned char>(*end)))
        ++end;
    if (*end != '\0')
        return false;

    if (out)
        *out = value;
    return true;
}

bool is_integral(const std::string& text)
{
    return text.find('.') == std::string::npos &&
           text.find('e') == std::string::npos &&
           text.find('E') == std::string::npos;
}

// A plain YAML scalar carries no type, so infer one the way a YAML 1.1 reader
// would for the subset LANCommander emits.
cJSON* typed_scalar(const std::string& text, bool plain)
{
    if (!plain)
        return cJSON_CreateString(text.c_str());

    if (text.empty() || text == "~" ||
        equals_ignore_case(text, "null"))
        return cJSON_CreateNull();

    if (equals_ignore_case(text, "true"))
        return cJSON_CreateTrue();
    if (equals_ignore_case(text, "false"))
        return cJSON_CreateFalse();

    double number = 0.0;
    if (looks_like_number(text, &number))
        return cJSON_CreateNumber(number);

    return cJSON_CreateString(text.c_str());
}

// Would this string be read back as something other than a string?
bool needs_quoting(const std::string& text)
{
    if (text.empty())
        return true;
    if (text == "~" || equals_ignore_case(text, "null") ||
        equals_ignore_case(text, "true") || equals_ignore_case(text, "false"))
        return true;
    return looks_like_number(text, NULL);
}

// --- YAML -> JSON ---------------------------------------------------------

// One level of the container stack. `key` holds a mapping's pending key: a
// mapping alternates key and value scalars, so we buffer the key until its
// value arrives.
struct Frame {
    cJSON* node;
    bool is_map;
    bool expecting_key;
    std::string key;

    Frame(cJSON* n, bool map)
        : node(n), is_map(map), expecting_key(map) {}
};

struct ParserGuard {
    yaml_parser_t* p;
    explicit ParserGuard(yaml_parser_t* parser) : p(parser) {}
    ~ParserGuard() { if (p) yaml_parser_delete(p); }
private:
    ParserGuard(const ParserGuard&);
    ParserGuard& operator=(const ParserGuard&);
};

struct JsonGuard {
    cJSON* node;
    explicit JsonGuard(cJSON* n) : node(n) {}
    ~JsonGuard() { if (node) cJSON_Delete(node); }
    cJSON* release() { cJSON* n = node; node = NULL; return n; }
private:
    JsonGuard(const JsonGuard&);
    JsonGuard& operator=(const JsonGuard&);
};

// Attaches a finished value to whatever container is open.
void attach(std::vector<Frame>& stack, cJSON* value)
{
    if (stack.empty() || !value)
        return;

    Frame& top = stack.back();

    if (top.is_map) {
        cJSON_AddItemToObject(top.node, top.key.c_str(), value);
        top.expecting_key = true;
    } else {
        cJSON_AddItemToArray(top.node, value);
    }
}

} // namespace

Result<std::string> to_json(const std::string& yaml_text)
{
    yaml_parser_t parser;
    if (!yaml_parser_initialize(&parser))
        return Result<std::string>::fail("could not initialise the YAML parser");

    ParserGuard parser_guard(&parser);
    yaml_parser_set_input_string(
        &parser, reinterpret_cast<const unsigned char*>(yaml_text.c_str()),
        yaml_text.size());

    std::vector<Frame> stack;
    JsonGuard root(NULL);
    bool document_done = false;

    for (;;) {
        yaml_event_t event;
        if (!yaml_parser_parse(&parser, &event)) {
            std::string message = "invalid YAML";
            if (parser.problem) {
                message += ": ";
                message += parser.problem;
            }
            return Result<std::string>::fail(message);
        }

        const yaml_event_type_t type = event.type;
        bool stop = false;

        switch (type) {
            case YAML_STREAM_END_EVENT:
                stop = true;
                break;

            case YAML_DOCUMENT_END_EVENT:
                // Only the first document is read; ignore any that follow.
                document_done = true;
                break;

            case YAML_ALIAS_EVENT:
                yaml_event_delete(&event);
                return Result<std::string>::fail(
                    "YAML anchors and aliases are not supported");

            case YAML_SCALAR_EVENT: {
                if (document_done)
                    break;

                const std::string text(
                    reinterpret_cast<const char*>(event.data.scalar.value),
                    event.data.scalar.length);

                if (!stack.empty() && stack.back().is_map &&
                    stack.back().expecting_key) {
                    stack.back().key = text;
                    stack.back().expecting_key = false;
                    break;
                }

                const bool plain =
                    event.data.scalar.style == YAML_PLAIN_SCALAR_STYLE;
                cJSON* value = typed_scalar(text, plain);

                if (stack.empty()) {
                    // A bare scalar document.
                    if (!root.node)
                        root.node = value;
                    else
                        cJSON_Delete(value);
                } else {
                    attach(stack, value);
                }
                break;
            }

            case YAML_MAPPING_START_EVENT:
            case YAML_SEQUENCE_START_EVENT: {
                if (document_done)
                    break;

                const bool is_map = (type == YAML_MAPPING_START_EVENT);
                cJSON* node = is_map ? cJSON_CreateObject() : cJSON_CreateArray();

                if (!node) {
                    yaml_event_delete(&event);
                    return Result<std::string>::fail("out of memory building JSON");
                }

                if (stack.empty()) {
                    if (root.node) {
                        cJSON_Delete(node);
                        break;   // a second document
                    }
                    root.node = node;
                } else {
                    attach(stack, node);
                }

                stack.push_back(Frame(node, is_map));
                break;
            }

            case YAML_MAPPING_END_EVENT:
            case YAML_SEQUENCE_END_EVENT:
                if (!stack.empty())
                    stack.pop_back();
                break;

            default:
                break;
        }

        yaml_event_delete(&event);

        if (stop)
            break;
    }

    if (!root.node)
        return Result<std::string>::ok("null");

    char* printed = cJSON_PrintUnformatted(root.node);
    if (!printed)
        return Result<std::string>::fail("could not serialize the parsed YAML");

    std::string out(printed);
    cJSON_free(printed);

    return Result<std::string>::ok(out);
}

// --- JSON -> YAML ---------------------------------------------------------

namespace {

int append_to_string(void* data, unsigned char* buffer, size_t size)
{
    static_cast<std::string*>(data)->append(
        reinterpret_cast<const char*>(buffer), size);
    return 1;
}

struct EmitterGuard {
    yaml_emitter_t* e;
    explicit EmitterGuard(yaml_emitter_t* emitter) : e(emitter) {}
    ~EmitterGuard() { if (e) yaml_emitter_delete(e); }
private:
    EmitterGuard(const EmitterGuard&);
    EmitterGuard& operator=(const EmitterGuard&);
};

bool emit_scalar(yaml_emitter_t* emitter, const std::string& text, bool quoted)
{
    yaml_event_t event;
    const yaml_scalar_style_t style =
        quoted ? YAML_SINGLE_QUOTED_SCALAR_STYLE : YAML_PLAIN_SCALAR_STYLE;

    if (!yaml_scalar_event_initialize(
            &event, NULL, NULL,
            reinterpret_cast<yaml_char_t*>(const_cast<char*>(text.c_str())),
            static_cast<int>(text.size()),
            quoted ? 0 : 1, 1, style))
        return false;

    return yaml_emitter_emit(emitter, &event) != 0;
}

std::string number_to_string(double value)
{
    char buffer[40];
    if (value == static_cast<double>(static_cast<long>(value)))
        std::sprintf(buffer, "%ld", static_cast<long>(value));
    else
        std::sprintf(buffer, "%g", value);
    return buffer;
}

bool emit_node(yaml_emitter_t* emitter, cJSON* node)
{
    if (!node)
        return emit_scalar(emitter, "null", false);

    yaml_event_t event;

    switch (node->type & 0xFF) {
        case cJSON_NULL:
            return emit_scalar(emitter, "null", false);

        case cJSON_True:
            return emit_scalar(emitter, "true", false);

        case cJSON_False:
            return emit_scalar(emitter, "false", false);

        case cJSON_Number:
            return emit_scalar(emitter, number_to_string(node->valuedouble), false);

        case cJSON_String: {
            const std::string text = node->valuestring ? node->valuestring : "";
            return emit_scalar(emitter, text, needs_quoting(text));
        }

        case cJSON_Array: {
            if (!yaml_sequence_start_event_initialize(
                    &event, NULL, NULL, 1, YAML_BLOCK_SEQUENCE_STYLE) ||
                !yaml_emitter_emit(emitter, &event))
                return false;

            for (cJSON* child = node->child; child; child = child->next) {
                if (!emit_node(emitter, child))
                    return false;
            }

            return yaml_sequence_end_event_initialize(&event) &&
                   yaml_emitter_emit(emitter, &event);
        }

        case cJSON_Object: {
            if (!yaml_mapping_start_event_initialize(
                    &event, NULL, NULL, 1, YAML_BLOCK_MAPPING_STYLE) ||
                !yaml_emitter_emit(emitter, &event))
                return false;

            for (cJSON* child = node->child; child; child = child->next) {
                const std::string key = child->string ? child->string : "";
                if (!emit_scalar(emitter, key, needs_quoting(key)))
                    return false;
                if (!emit_node(emitter, child))
                    return false;
            }

            return yaml_mapping_end_event_initialize(&event) &&
                   yaml_emitter_emit(emitter, &event);
        }

        default:
            return emit_scalar(emitter, "null", false);
    }
}

} // namespace

Result<std::string> from_json(const std::string& json_text)
{
    cJSON* parsed = cJSON_Parse(json_text.c_str());
    if (!parsed)
        return Result<std::string>::fail("invalid JSON");

    JsonGuard json(parsed);

    yaml_emitter_t emitter;
    if (!yaml_emitter_initialize(&emitter))
        return Result<std::string>::fail("could not initialise the YAML emitter");

    EmitterGuard emitter_guard(&emitter);

    std::string out;
    yaml_emitter_set_output(&emitter, append_to_string, &out);
    yaml_emitter_set_unicode(&emitter, 1);

    yaml_event_t event;

    if (!yaml_stream_start_event_initialize(&event, YAML_UTF8_ENCODING) ||
        !yaml_emitter_emit(&emitter, &event))
        return Result<std::string>::fail("could not start the YAML stream");

    if (!yaml_document_start_event_initialize(&event, NULL, NULL, NULL, 1) ||
        !yaml_emitter_emit(&emitter, &event))
        return Result<std::string>::fail("could not start the YAML document");

    if (!emit_node(&emitter, json.node))
        return Result<std::string>::fail("could not emit YAML");

    if (!yaml_document_end_event_initialize(&event, 1) ||
        !yaml_emitter_emit(&emitter, &event))
        return Result<std::string>::fail("could not end the YAML document");

    if (!yaml_stream_end_event_initialize(&event) ||
        !yaml_emitter_emit(&emitter, &event))
        return Result<std::string>::fail("could not end the YAML stream");

    return Result<std::string>::ok(out);
}

} // namespace yaml
} // namespace lancommander
