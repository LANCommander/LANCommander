#include "script/cmdlets/cmdlet_support.h"

#include "cJSON.h"

#include <cstdio>
#include <cstdlib>

namespace lancommander {
namespace cmdlets {

namespace {

// Fetches a parameter by name, falling back to a positional slot.
bool fetch(PicoStage* ctx, const char* name, int position, pico_value* out)
{
    if (name && pico_bp_named(&ctx->bp, name, out))
        return true;
    if (position >= 0 && pico_bp_pos(&ctx->bp, position, out))
        return true;
    return false;
}

std::string to_std_string(pico_value v)
{
    PicoStr* s = pico_val_to_str(v);
    if (!s)
        return std::string();

    std::string out(pico_str_cstr(s));
    pico_str_release(s);

    return out;
}

// The inverse of json_to_value below.
cJSON* value_to_cjson(pico_value v)
{
    switch (v.type) {
        case PICO_VT_NULL:
            return cJSON_CreateNull();

        case PICO_VT_BOOL:
            return v.u.b ? cJSON_CreateTrue() : cJSON_CreateFalse();

        case PICO_VT_INT:
            return cJSON_CreateNumber((double)v.u.i);

        case PICO_VT_DOUBLE:
            return cJSON_CreateNumber(v.u.d);

        case PICO_VT_STR: {
            PicoStr* s = pico_val_to_str(v);
            cJSON* node = cJSON_CreateString(s ? pico_str_cstr(s) : "");
            if (s)
                pico_str_release(s);
            return node;
        }

        case PICO_VT_ARRAY: {
            cJSON* array = cJSON_CreateArray();
            if (!array || !v.u.a)
                return array;

            const int count = pico_array_count(v.u.a);
            for (int i = 0; i < count; ++i) {
                pico_value item;
                if (pico_array_get(v.u.a, i, &item)) {
                    cJSON_AddItemToArray(array, value_to_cjson(item));
                    pico_val_release(item);
                }
            }
            return array;
        }

        case PICO_VT_OBJECT: {
            cJSON* object = cJSON_CreateObject();
            if (!object || !v.u.o)
                return object;

            for (PicoProp* prop = v.u.o->props; prop; prop = prop->next)
                cJSON_AddItemToObject(object, prop->name ? prop->name : "",
                                      value_to_cjson(prop->val));
            return object;
        }

        default:
            return cJSON_CreateNull();
    }
}

pico_value json_to_value(cJSON* node)
{
    if (!node)
        return pico_val_null();

    switch (node->type & 0xFF) {
        case cJSON_False:
            return pico_val_bool(0);

        case cJSON_True:
            return pico_val_bool(1);

        case cJSON_Number:
            if (node->valuedouble == (double)(long)node->valuedouble)
                return pico_val_int((long)node->valuedouble);
            return pico_val_double(node->valuedouble);

        case cJSON_String:
            return pico_val_cstr(node->valuestring ? node->valuestring : "");

        case cJSON_Array: {
            PicoArray* array = pico_array_new();
            if (!array)
                return pico_val_null();

            for (cJSON* child = node->child; child; child = child->next) {
                pico_value item = json_to_value(child);
                pico_array_push(array, item);
                pico_val_release(item);
            }

            return pico_val_take_array(array);
        }

        case cJSON_Object: {
            PicoObject* object = pico_object_new();
            if (!object)
                return pico_val_null();

            for (cJSON* child = node->child; child; child = child->next) {
                pico_value item = json_to_value(child);
                pico_object_set(object, child->string ? child->string : "", item);
                pico_val_release(item);
            }

            return pico_val_take_object(object);
        }

        default:
            return pico_val_null();
    }
}

} // namespace

bool arg_string(PicoStage* ctx, const char* name, int position, std::string* out)
{
    pico_value v;
    if (!fetch(ctx, name, position, &v))
        return false;

    if (out)
        *out = to_std_string(v);
    pico_val_release(v);

    return true;
}

bool arg_long(PicoStage* ctx, const char* name, int position, long* out)
{
    pico_value v;
    if (!fetch(ctx, name, position, &v))
        return false;

    if (out)
        *out = pico_val_to_long(v);
    pico_val_release(v);

    return true;
}

bool arg_double(PicoStage* ctx, const char* name, int position, double* out)
{
    pico_value v;
    if (!fetch(ctx, name, position, &v))
        return false;

    if (out)
        *out = pico_val_to_double(v);
    pico_val_release(v);

    return true;
}

bool arg_bool(PicoStage* ctx, const char* name, bool* out)
{
    // Accept the switch form too, so `-Utf16` behaves like `-Utf16 1` for
    // anyone who writes it that way.
    if (pico_bp_switch(&ctx->bp, name)) {
        if (out)
            *out = true;
        return true;
    }

    pico_value v;
    if (!pico_bp_named(&ctx->bp, name, &v))
        return false;

    if (out)
        *out = pico_val_truthy(v) != 0;
    pico_val_release(v);

    return true;
}

bool arg_bytes(PicoStage* ctx, const char* name, int position,
               std::vector<unsigned char>* out)
{
    pico_value v;
    if (!fetch(ctx, name, position, &v))
        return false;

    if (out) {
        out->clear();

        if (v.type == PICO_VT_ARRAY && v.u.a) {
            const int count = pico_array_count(v.u.a);
            for (int i = 0; i < count; ++i) {
                pico_value item;
                if (pico_array_get(v.u.a, i, &item)) {
                    out->push_back((unsigned char)(pico_val_to_long(item) & 0xFF));
                    pico_val_release(item);
                }
            }
        } else {
            const std::string text = to_std_string(v);
            for (std::size_t i = 0; i < text.size(); ++i)
                out->push_back((unsigned char)text[i]);
        }
    }

    pico_val_release(v);
    return true;
}

pico_status emit_string(PicoStage* ctx, const std::string& value)
{
    pico_value v = pico_val_cstr(value.c_str());
    const pico_status status = pico_emit(ctx, v);
    pico_val_release(v);
    return status;
}

pico_status emit_long(PicoStage* ctx, long value)
{
    pico_value v = pico_val_int(value);
    const pico_status status = pico_emit(ctx, v);
    pico_val_release(v);
    return status;
}

pico_status emit_bool(PicoStage* ctx, bool value)
{
    pico_value v = pico_val_bool(value ? 1 : 0);
    const pico_status status = pico_emit(ctx, v);
    pico_val_release(v);
    return status;
}

pico_status emit_bytes(PicoStage* ctx, const std::vector<unsigned char>& bytes)
{
    PicoArray* array = pico_array_new();
    if (!array)
        return PICO_ERR_RUNTIME;

    for (std::size_t i = 0; i < bytes.size(); ++i) {
        pico_value item = pico_val_int((long)bytes[i]);
        pico_array_push(array, item);
        pico_val_release(item);
    }

    pico_value v = pico_val_take_array(array);
    const pico_status status = pico_emit(ctx, v);
    pico_val_release(v);

    return status;
}

pico_status emit_json(PicoStage* ctx, const std::string& json)
{
    cJSON* parsed = cJSON_Parse(json.c_str());
    if (!parsed)
        return fail(ctx, PICO_ERR_RUNTIME, "could not read the document");

    pico_value v = json_to_value(parsed);
    cJSON_Delete(parsed);

    const pico_status status = pico_emit(ctx, v);
    pico_val_release(v);

    return status;
}

std::string value_to_json(pico_value v)
{
    cJSON* node = value_to_cjson(v);
    if (!node)
        return "null";

    char* printed = cJSON_PrintUnformatted(node);
    const std::string out = printed ? std::string(printed) : std::string("null");

    if (printed)
        cJSON_free(printed);
    cJSON_Delete(node);

    return out;
}

pico_status fail(PicoStage* ctx, pico_status status, const std::string& message)
{
    const char* name = (ctx && ctx->def && ctx->def->name) ? ctx->def->name
                                                           : "cmdlet";
    const std::string text = std::string(name) + ": " + message;

    cmd_fail(ctx, status, text.c_str());

    return status;
}

bool set_variable(PicoStage* ctx, const char* name, const std::string& value)
{
    if (!ctx || !ctx->interp)
        return false;

    pico_value v = pico_val_cstr(value.c_str());
    const int rc = pico_scope_set(ctx->interp, name, v);
    pico_val_release(v);

    return rc == 0;
}

bool set_variable_json(PicoStage* ctx, const char* name, const std::string& json)
{
    if (!ctx || !ctx->interp)
        return false;

    cJSON* parsed = cJSON_Parse(json.c_str());
    if (!parsed)
        return false;

    pico_value v = json_to_value(parsed);
    cJSON_Delete(parsed);

    const int rc = pico_scope_set(ctx->interp, name, v);
    pico_val_release(v);

    return rc == 0;
}

} // namespace cmdlets
} // namespace lancommander
