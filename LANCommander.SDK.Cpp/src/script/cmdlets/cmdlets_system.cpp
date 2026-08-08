// Get-Runtime, Get-SanitizedPath, Get-PrimaryDisplay.

#include "script/cmdlets/cmdlet_support.h"

#include "lancommander/script/script_helper.h"

#ifdef _WIN32
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#include <windows.h>
#endif

#include <cctype>
#include <cstring>
#include <string>

namespace lancommander {
namespace cmdlets {

namespace {

// --- Get-Runtime ----------------------------------------------------------

pico_status get_runtime_begin(PicoStage* ctx)
{
    // The .NET cmdlet writes the RuntimePlatform enum, which renders as its
    // name. Emit the name directly so `-eq 'Windows'` works in a script.
    switch (script::current_runtime_platform()) {
        case RuntimePlatform_Windows: return emit_string(ctx, "Windows");
        case RuntimePlatform_Linux:   return emit_string(ctx, "Linux");
        case RuntimePlatform_macOS:   return emit_string(ctx, "macOS");
        default:                      return emit_string(ctx, "None");
    }
}

const PicoCmdletDef get_runtime_def = {
    "Get-Runtime", NULL, 0, get_runtime_begin, NULL, NULL,
    "Returns the current runtime platform (Windows, Linux, macOS)."
};

// --- Get-SanitizedPath ----------------------------------------------------

// Path.GetInvalidFileNameChars() on Windows: the control characters plus
// " * / : < > ? \ |. Applied on every platform so a name sanitised on Linux is
// still valid when the same game is installed on Windows.
bool is_invalid_filename_char(unsigned char c)
{
    if (c < 32)
        return true;
    switch (c) {
        case '"': case '*': case '/': case ':':
        case '<': case '>': case '?': case '\\': case '|':
            return true;
        default:
            return false;
    }
}

bool is_word_char(unsigned char c)
{
    return std::isalnum(c) || c == '_';
}

std::string sanitize_filename(const std::string& input)
{
    // First the colon-in-title rule, `(\w)(: )(\w)` -> `$1 - $3`, so
    // "Half-Life: Opposing Force" becomes "Half-Life - Opposing Force" rather
    // than losing the colon entirely.
    std::string spaced;
    spaced.reserve(input.size() + 8);

    for (std::size_t i = 0; i < input.size(); ++i) {
        if (input[i] == ':' && i + 2 < input.size() && input[i + 1] == ' ' &&
            i > 0 && is_word_char((unsigned char)input[i - 1]) &&
            is_word_char((unsigned char)input[i + 2])) {
            spaced += " - ";
            ++i;                       // also consume the space
            continue;
        }
        spaced += input[i];
    }

    std::string out;
    out.reserve(spaced.size());
    for (std::size_t i = 0; i < spaced.size(); ++i) {
        if (!is_invalid_filename_char((unsigned char)spaced[i]))
            out += spaced[i];
    }

    if (!out.empty() && out[out.size() - 1] == '.')
        out.erase(out.size() - 1);

    return out;
}

pico_status get_sanitized_path_begin(PicoStage* ctx)
{
    // picoposh runs begin for every stage, including one that is about to
    // receive piped input, so an absent -Path is not an error here — it means
    // the values are coming through process(). Failing instead would break
    // `'Half-Life: Opposing Force' | Get-SanitizedPath`. Same shape as the
    // built-in Write-Output.
    std::string path;
    if (!arg_string(ctx, "Path", 0, &path))
        return PICO_OK;

    return emit_string(ctx, sanitize_filename(path));
}

pico_status get_sanitized_path_process(PicoStage* ctx, pico_value in)
{
    PicoStr* s = pico_val_to_str(in);
    if (!s)
        return PICO_OK;

    const std::string text(pico_str_cstr(s));
    pico_str_release(s);

    return emit_string(ctx, sanitize_filename(text));
}

const PicoParamDef get_sanitized_path_params[] = {
    { "Path", 0, 0 }
};

const PicoCmdletDef get_sanitized_path_def = {
    "Get-SanitizedPath", get_sanitized_path_params, 1,
    get_sanitized_path_begin, get_sanitized_path_process, NULL,
    "Strips characters that cannot appear in a filename."
};

// --- Get-PrimaryDisplay ---------------------------------------------------

struct DisplayInfo {
    long width;
    long height;
    long refresh_rate;
    long bits_per_pixel;

    DisplayInfo() : width(0), height(0), refresh_rate(0), bits_per_pixel(0) {}
};

// Mirrors DisplayHelper.GetScreen. EnumDisplaySettingsA has been available
// since Windows 95; elsewhere the fields stay zero, as they do in the .NET SDK
// when no display source can be read.
DisplayInfo current_display()
{
    DisplayInfo info;

#ifdef _WIN32
    DEVMODEA mode;
    std::memset(&mode, 0, sizeof(mode));
    mode.dmSize = sizeof(mode);

    if (EnumDisplaySettingsA(NULL, ENUM_CURRENT_SETTINGS, &mode)) {
        info.width = (long)mode.dmPelsWidth;
        info.height = (long)mode.dmPelsHeight;
        info.refresh_rate = (long)mode.dmDisplayFrequency;
        info.bits_per_pixel = (long)mode.dmBitsPerPel;
    }
#endif

    return info;
}

pico_status get_primary_display_begin(PicoStage* ctx)
{
    const DisplayInfo info = current_display();

    PicoObject* screen = pico_object_new();
    if (!screen)
        return fail(ctx, PICO_ERR_RUNTIME, "out of memory");

    pico_value v;

    v = pico_val_bool(1);
    pico_object_set(screen, "Primary", v);
    pico_val_release(v);

    v = pico_val_int(info.width);
    pico_object_set(screen, "Width", v);
    pico_val_release(v);

    v = pico_val_int(info.height);
    pico_object_set(screen, "Height", v);
    pico_val_release(v);

    v = pico_val_int(info.refresh_rate);
    pico_object_set(screen, "RefreshRate", v);
    pico_val_release(v);

    v = pico_val_int(info.bits_per_pixel);
    pico_object_set(screen, "BitsPerPixel", v);
    pico_val_release(v);

    pico_value out = pico_val_take_object(screen);
    const pico_status status = pico_emit(ctx, out);
    pico_val_release(out);

    return status;
}

const PicoCmdletDef get_primary_display_def = {
    "Get-PrimaryDisplay", NULL, 0, get_primary_display_begin, NULL, NULL,
    "Returns the primary display's Width, Height, RefreshRate and BitsPerPixel."
};

} // namespace

const PicoCmdletDef* cmdlet_get_runtime()         { return &get_runtime_def; }
const PicoCmdletDef* cmdlet_get_sanitized_path()  { return &get_sanitized_path_def; }
const PicoCmdletDef* cmdlet_get_primary_display() { return &get_primary_display_def; }

// Display metrics are shared with the FOV cmdlets.
void primary_display_size(long* width, long* height)
{
    const DisplayInfo info = current_display();
    if (width)  *width = info.width;
    if (height) *height = info.height;
}

} // namespace cmdlets
} // namespace lancommander
