// image_decoder.cpp — image loading via stb_image.
//
// Replaces the GDI+ implementation. GDI+ is Windows-only and does not ship
// with Windows 95/98, so the old build had to bundle gdiplus.dll as a
// redistributable; stb is portable C with no dependencies at all, which also
// keeps the DOS path open.
//
// The DecodedImage contract (tightly packed RGBA, caller frees) is unchanged,
// so image_cache, screen_login and window_chrome are unaffected.
//
// One behavioural difference worth knowing: GDI+ could decode a JPEG straight
// to a target size, whereas stb always decodes at full resolution and we
// downscale afterwards. That is why the bundled backgrounds are pre-shrunk —
// see tools/reencode-backgrounds.ps1.

#include "ui/image_decoder.h"

#include "stb_image.h"
#include "stb_image_resize2.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

namespace
{
    // Aspect-preserving fit, unchanged from the GDI+ version.
    void fit_size(int src_w, int src_h, int max_w, int max_h,
                  int *dst_w, int *dst_h)
    {
        int w = src_w;
        int h = src_h;

        if (w > max_w)
        {
            h = h * max_w / w;
            w = max_w;
        }
        if (h > max_h)
        {
            w = w * max_h / h;
            h = max_h;
        }
        if (w <= 0) w = 1;
        if (h <= 0) h = 1;

        *dst_w = w;
        *dst_h = h;
    }

    bool read_file(const char *path, std::vector<unsigned char> *out)
    {
        FILE *f = fopen(path, "rb");
        if (!f)
            return false;

        if (fseek(f, 0, SEEK_END) != 0)
        {
            fclose(f);
            return false;
        }

        long size = ftell(f);
        if (size <= 0)
        {
            fclose(f);
            return false;
        }
        rewind(f);

        out->resize((size_t)size);
        size_t got = fread(&(*out)[0], 1, (size_t)size, f);
        fclose(f);

        return got == (size_t)size;
    }
} // namespace

void image_decoder_init()
{
    // Nothing to do — kept so callers don't change.
}

void image_decoder_shutdown()
{
}

bool decode_image_memory(const void *data, int data_size, int max_w, int max_h,
                         DecodedImage *out)
{
    if (!data || data_size <= 0 || !out)
        return false;

    int src_w = 0, src_h = 0, channels = 0;
    unsigned char *pixels = stbi_load_from_memory(
        (const stbi_uc *)data, data_size, &src_w, &src_h, &channels, 4);

    if (!pixels || src_w <= 0 || src_h <= 0)
    {
        if (pixels)
            stbi_image_free(pixels);
        return false;
    }

    int dst_w = 0, dst_h = 0;
    fit_size(src_w, src_h, max_w, max_h, &dst_w, &dst_h);

    if (dst_w == src_w && dst_h == src_h)
    {
        // No resampling needed. stb's allocation is plain malloc, which is
        // what free_decoded_image releases, so hand it over directly.
        out->pixels = pixels;
        out->width = src_w;
        out->height = src_h;
        return true;
    }

    unsigned char *scaled = (unsigned char *)malloc((size_t)dst_w * dst_h * 4);
    if (!scaled)
    {
        stbi_image_free(pixels);
        return false;
    }

    // Mitchell by default for downscale — visually equivalent to the GDI+
    // HighQualityBicubic this replaces, and sRGB-correct, which GDI+ was not.
    unsigned char *ok = stbir_resize_uint8_srgb(
        pixels, src_w, src_h, 0,
        scaled, dst_w, dst_h, 0,
        STBIR_RGBA);

    stbi_image_free(pixels);

    if (!ok)
    {
        free(scaled);
        return false;
    }

    out->pixels = scaled;
    out->width = dst_w;
    out->height = dst_h;
    return true;
}

bool decode_image_file(const char *path, int max_w, int max_h, DecodedImage *out)
{
    if (!path || !out)
        return false;

    std::vector<unsigned char> buf;
    if (!read_file(path, &buf) || buf.empty())
        return false;

    return decode_image_memory(&buf[0], (int)buf.size(), max_w, max_h, out);
}

bool decode_image_asset(const char *name, int max_w, int max_h, DecodedImage *out)
{
    if (!name)
        return false;

    // Assets ship alongside the executable. This replaces the old
    // FindResourceA/RT_RCDATA lookup, which was Win32-only.
    std::string path = std::string("assets/") + name;
    return decode_image_file(path.c_str(), max_w, max_h, out);
}

void free_decoded_image(DecodedImage *img)
{
    if (!img)
        return;
    if (img->pixels)
        free(img->pixels);
    img->pixels = NULL;
    img->width = 0;
    img->height = 0;
}
