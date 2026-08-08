// ConvertTo-StringBytes and Edit-PatchBinary — the pair used together to patch
// a player name into a save file or executable.

#include "script/cmdlets/cmdlet_defs.h"

#include <cstdio>
#include <string>
#include <vector>

namespace lancommander {
namespace cmdlets {

namespace {

// --- ConvertTo-StringBytes ------------------------------------------------

pico_status convert_to_string_bytes_begin(PicoStage* ctx)
{
    std::string input;
    if (!arg_string(ctx, "Input", 0, &input))
        return fail(ctx, PICO_ERR_ARG, "requires an -Input");

    bool utf16 = false;
    bool big_endian = false;
    long max_length = 0;
    long min_length = 0;

    arg_bool(ctx, "Utf16", &utf16);
    arg_bool(ctx, "BigEndian", &big_endian);
    arg_long(ctx, "MaxLength", -1, &max_length);
    arg_long(ctx, "MinLength", -1, &min_length);

    if (max_length > 0 && (long)input.size() > max_length)
        input = input.substr(0, (std::size_t)max_length);

    // Reproduces the .NET padding logic verbatim, quirk included: when
    // MinLength >= MaxLength it pads to MaxLength, not MinLength. So
    // `-MaxLength 10 -MinLength 10` gives 10, but `-MinLength 10` alone (with
    // MaxLength 0) pads to 0, i.e. not at all.
    if (min_length > 0 && min_length < max_length)
        input.resize((std::size_t)min_length, '\0');
    else if (min_length > 0)
        input.resize((std::size_t)max_length, '\0');

    std::vector<unsigned char> bytes;

    if (utf16) {
        bytes.reserve(input.size() * 2);
        for (std::size_t i = 0; i < input.size(); ++i) {
            const unsigned char c = (unsigned char)input[i];
            if (big_endian) {
                bytes.push_back(0);
                bytes.push_back(c);
            } else {
                bytes.push_back(c);
                bytes.push_back(0);
            }
        }
    } else {
        bytes.reserve(input.size());
        for (std::size_t i = 0; i < input.size(); ++i) {
            const unsigned char c = (unsigned char)input[i];
            // Encoding.ASCII maps anything above 0x7F to '?'.
            bytes.push_back(c > 0x7F ? (unsigned char)'?' : c);
        }
    }

    return emit_bytes(ctx, bytes);
}

const PicoParamDef convert_to_string_bytes_params[] = {
    { "Input", 0, 0 },      { "Utf16", 0, -1 }, { "BigEndian", 0, -1 },
    { "MaxLength", 0, -1 }, { "MinLength", 0, -1 }
};

const PicoCmdletDef convert_to_string_bytes_def = {
    "ConvertTo-StringBytes", convert_to_string_bytes_params, 5,
    convert_to_string_bytes_begin, NULL, NULL,
    "Converts a string to a byte array (ASCII, or UTF-16 with -Utf16)."
};

// --- Edit-PatchBinary -----------------------------------------------------

pico_status edit_patch_binary_begin(PicoStage* ctx)
{
    long offset = 0;
    std::vector<unsigned char> data;
    std::string file_path;

    if (!arg_long(ctx, "Offset", 0, &offset))
        return fail(ctx, PICO_ERR_ARG, "requires an -Offset");
    if (!arg_bytes(ctx, "Data", 1, &data))
        return fail(ctx, PICO_ERR_ARG, "requires -Data");
    if (!arg_string(ctx, "FilePath", 2, &file_path))
        return fail(ctx, PICO_ERR_ARG, "requires a -FilePath");

    if (offset < 0)
        return fail(ctx, PICO_ERR_ARG, "-Offset cannot be negative");

    // "r+b" rather than "wb": this patches bytes in place and must not
    // truncate the rest of the file, matching File.OpenWrite + Seek.
    std::FILE* file = std::fopen(file_path.c_str(), "r+b");
    if (!file)
        return fail(ctx, PICO_ERR_IO, "could not open " + file_path);

    if (std::fseek(file, offset, SEEK_SET) != 0) {
        std::fclose(file);
        return fail(ctx, PICO_ERR_IO, "could not seek in " + file_path);
    }

    const std::size_t written =
        data.empty() ? 0 : std::fwrite(&data[0], 1, data.size(), file);
    std::fclose(file);

    if (written != data.size())
        return fail(ctx, PICO_ERR_IO, "short write to " + file_path);

    return PICO_OK;   // the .NET cmdlet emits nothing
}

const PicoParamDef edit_patch_binary_params[] = {
    { "Offset", 0, 0 }, { "Data", 0, 1 }, { "FilePath", 0, 2 }
};

const PicoCmdletDef edit_patch_binary_def = {
    "Edit-PatchBinary", edit_patch_binary_params, 3,
    edit_patch_binary_begin, NULL, NULL,
    "Writes bytes at a byte offset in a file, leaving the rest intact."
};

} // namespace

const PicoCmdletDef* cmdlet_convert_to_string_bytes()
{
    return &convert_to_string_bytes_def;
}

const PicoCmdletDef* cmdlet_edit_patch_binary() { return &edit_patch_binary_def; }

} // namespace cmdlets
} // namespace lancommander
