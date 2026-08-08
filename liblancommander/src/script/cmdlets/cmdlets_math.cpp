// Convert-AspectRatio, Get-HorizontalFov, Get-VerticalFov.
//
// These exist so a script can adapt a game built for 4:3 to the player's actual
// display without hard-coding resolutions.

#include "script/cmdlets/cmdlet_defs.h"

#include <cmath>

namespace lancommander {
namespace cmdlets {

namespace {

const double kBaseAspectRatio = 4.0 / 3.0;
const double kPi = 3.14159265358979323846;

// --- Convert-AspectRatio --------------------------------------------------

pico_status convert_aspect_ratio_begin(PicoStage* ctx)
{
    long width = 0;
    long height = 0;
    double aspect_ratio = 0.0;

    if (!arg_long(ctx, "Width", 0, &width))
        return fail(ctx, PICO_ERR_ARG, "requires a -Width");
    if (!arg_long(ctx, "Height", 1, &height))
        return fail(ctx, PICO_ERR_ARG, "requires a -Height");
    if (!arg_double(ctx, "AspectRatio", 2, &aspect_ratio))
        return fail(ctx, PICO_ERR_ARG, "requires an -AspectRatio");

    if (height == 0)
        return fail(ctx, PICO_ERR_ARG, "-Height cannot be zero");
    if (aspect_ratio == 0.0)
        return fail(ctx, PICO_ERR_ARG, "-AspectRatio cannot be zero");

    long out_width = width;
    long out_height = height;

    // Reproduces the .NET version exactly, including its integer division in
    // the comparison — (Width / Height) is int division there, so e.g.
    // 2560x1440 compares as 1, not 1.777. Changing it would silently give
    // different resolutions than the .NET launcher for the same inputs.
    if ((width / height) < aspect_ratio) {
        // Display is wider than the target: pillarbox.
        out_width = (long)std::ceil((double)height * aspect_ratio);
        out_height = height;
    } else {
        // Letterbox.
        out_width = width;
        out_height = (long)std::ceil((double)width * (1.0 / aspect_ratio));
    }

    PicoObject* resolution = pico_object_new();
    if (!resolution)
        return fail(ctx, PICO_ERR_RUNTIME, "out of memory");

    pico_value v;

    v = pico_val_int(out_width);
    pico_object_set(resolution, "Width", v);
    pico_val_release(v);

    v = pico_val_int(out_height);
    pico_object_set(resolution, "Height", v);
    pico_val_release(v);

    pico_value result = pico_val_take_object(resolution);
    const pico_status status = pico_emit(ctx, result);
    pico_val_release(result);

    return status;
}

const PicoParamDef convert_aspect_ratio_params[] = {
    { "Width", 0, 0 }, { "Height", 0, 1 }, { "AspectRatio", 0, 2 }
};

const PicoCmdletDef convert_aspect_ratio_def = {
    "Convert-AspectRatio", convert_aspect_ratio_params, 3,
    convert_aspect_ratio_begin, NULL, NULL,
    "Fits a resolution to a target aspect ratio by pillar- or letterboxing."
};

// --- the FOV pair ---------------------------------------------------------

// Both default to the primary display when a dimension is omitted.
bool resolve_dimensions(PicoStage* ctx, long* width, long* height)
{
    *width = 0;
    *height = 0;

    arg_long(ctx, "Width", -1, width);
    arg_long(ctx, "Height", -1, height);

    if (*width == 0 || *height == 0) {
        long display_width = 0;
        long display_height = 0;
        primary_display_size(&display_width, &display_height);

        if (*width == 0)
            *width = display_width;
        if (*height == 0)
            *height = display_height;
    }

    return *width > 0 && *height > 0;
}

pico_status get_horizontal_fov_begin(PicoStage* ctx)
{
    long width = 0;
    long height = 0;
    long base_fov = 90;

    arg_long(ctx, "BaseFov", -1, &base_fov);

    if (!resolve_dimensions(ctx, &width, &height)) {
        return fail(ctx, PICO_ERR_RUNTIME,
                    "could not determine the display size; pass -Width and -Height");
    }

    const double current = (double)width / (double)height;
    const double base_radians = kPi * (double)base_fov / 180.0;
    const double scaled_radians =
        2.0 * std::atan(std::tan(base_radians / 2.0) * (current / kBaseAspectRatio));

    return emit_long(ctx, (long)(scaled_radians * 180.0 / kPi + 0.5));
}

const PicoParamDef fov_params[] = {
    { "Width", 0, -1 }, { "Height", 0, -1 }, { "BaseFov", 0, -1 }
};

const PicoCmdletDef get_horizontal_fov_def = {
    "Get-HorizontalFov", fov_params, 3, get_horizontal_fov_begin, NULL, NULL,
    "Scales a 4:3 horizontal FOV to the display's aspect ratio."
};

pico_status get_vertical_fov_begin(PicoStage* ctx)
{
    long width = 0;
    long height = 0;
    long base_fov = 75;

    arg_long(ctx, "BaseFov", -1, &base_fov);

    if (!resolve_dimensions(ctx, &width, &height)) {
        return fail(ctx, PICO_ERR_RUNTIME,
                    "could not determine the display size; pass -Width and -Height");
    }

    const double current = (double)width / (double)height;
    const double radians =
        2.0 * std::atan(std::tan(((double)base_fov * kPi / 180.0) / 2.0) *
                        (kBaseAspectRatio / current));

    return emit_long(ctx, (long)(radians * 180.0 / kPi + 0.5));
}

const PicoCmdletDef get_vertical_fov_def = {
    "Get-VerticalFov", fov_params, 3, get_vertical_fov_begin, NULL, NULL,
    "Scales a 4:3 vertical FOV to the display's aspect ratio."
};

} // namespace

const PicoCmdletDef* cmdlet_convert_aspect_ratio() { return &convert_aspect_ratio_def; }
const PicoCmdletDef* cmdlet_get_horizontal_fov()   { return &get_horizontal_fov_def; }
const PicoCmdletDef* cmdlet_get_vertical_fov()     { return &get_vertical_fov_def; }

} // namespace cmdlets
} // namespace lancommander
