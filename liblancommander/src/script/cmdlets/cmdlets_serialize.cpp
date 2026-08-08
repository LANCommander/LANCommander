// ConvertTo-SerializedBase64 and ConvertFrom-SerializedBase64 — moving a
// structured value through a text-only channel.
//
// The wire format is base64 of UTF-8 YAML, matching the .NET pair exactly, so a
// value serialised by either launcher deserialises in the other. Everything
// here is a short hop between existing pieces: the value becomes JSON
// (cmdlet_support), the JSON becomes YAML (src/yaml), and the YAML becomes
// base64.

#include "script/cmdlets/cmdlet_defs.h"

#include "util/base64.h"
#include "yaml/yaml_convert.h"

#include <string>

namespace lancommander {
namespace cmdlets {

namespace {

// --- ConvertTo-SerializedBase64 -------------------------------------------

pico_status serialize_value(PicoStage* ctx, pico_value v)
{
    Result<std::string> yaml_text = yaml::from_json(value_to_json(v));
    if (!yaml_text)
        return fail(ctx, PICO_ERR_RUNTIME, yaml_text.error);

    return emit_string(ctx, base64::encode(yaml_text.value));
}

pico_status convert_to_serialized_base64_begin(PicoStage* ctx)
{
    // picoposh runs begin for every stage, including one that is about to
    // receive piped input, so an absent -Input is not an error here — it just
    // means the values are coming through process(). Same shape as the
    // built-in Write-Output.
    pico_value input;
    if (!pico_bp_named(&ctx->bp, "Input", &input) &&
        !pico_bp_pos(&ctx->bp, 0, &input))
        return PICO_OK;

    const pico_status status = serialize_value(ctx, input);
    pico_val_release(input);

    return status;
}

pico_status convert_to_serialized_base64_process(PicoStage* ctx, pico_value in)
{
    return serialize_value(ctx, in);
}

const PicoParamDef convert_to_serialized_base64_params[] = {
    { "Input", 0, 0 }
};

const PicoCmdletDef convert_to_serialized_base64_def = {
    "ConvertTo-SerializedBase64", convert_to_serialized_base64_params, 1,
    convert_to_serialized_base64_begin, convert_to_serialized_base64_process, NULL,
    "Serializes a value to YAML and encodes it as base64."
};

// --- ConvertFrom-SerializedBase64 -----------------------------------------

pico_status deserialize_text(PicoStage* ctx, const std::string& encoded)
{
    std::string yaml_text;
    if (!base64::decode(encoded, &yaml_text))
        return fail(ctx, PICO_ERR_ARG, "the input is not valid base64");

    Result<std::string> json = yaml::to_json(yaml_text);
    if (!json)
        return fail(ctx, PICO_ERR_RUNTIME,
                    "the decoded input is not valid YAML: " + json.error);

    return emit_json(ctx, json.value);
}

pico_status convert_from_serialized_base64_begin(PicoStage* ctx)
{
    std::string encoded;
    if (!arg_string(ctx, "Input", 0, &encoded))
        return PICO_OK;   // piped input arrives through process()

    return deserialize_text(ctx, encoded);
}

pico_status convert_from_serialized_base64_process(PicoStage* ctx, pico_value in)
{
    PicoStr* s = pico_val_to_str(in);
    if (!s)
        return PICO_OK;

    const std::string encoded(pico_str_cstr(s));
    pico_str_release(s);

    return deserialize_text(ctx, encoded);
}

const PicoParamDef convert_from_serialized_base64_params[] = {
    { "Input", 0, 0 }
};

const PicoCmdletDef convert_from_serialized_base64_def = {
    "ConvertFrom-SerializedBase64", convert_from_serialized_base64_params, 1,
    convert_from_serialized_base64_begin, convert_from_serialized_base64_process,
    NULL,
    "Decodes base64 YAML back into a value."
};

} // namespace

const PicoCmdletDef* cmdlet_convert_to_serialized_base64()
{
    return &convert_to_serialized_base64_def;
}

const PicoCmdletDef* cmdlet_convert_from_serialized_base64()
{
    return &convert_from_serialized_base64_def;
}

} // namespace cmdlets
} // namespace lancommander
