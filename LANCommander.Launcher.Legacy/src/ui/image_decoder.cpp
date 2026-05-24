// image_decoder.cpp — GDI+ based image decoder.
// This file must NOT include any Allegro headers because Windows' BITMAP
// typedef conflicts with Allegro 4's BITMAP struct.

#include "ui/image_decoder.h"

#define NOMINMAX
#include <windows.h>
#include <gdiplus.h>

#include <cstdlib>
#include <cstring>
#include <string>

#pragma comment(lib, "gdiplus.lib")

static ULONG_PTR s_gdiplus_token = 0;
static bool s_gdiplus_ready = false;

static void ensure_gdiplus()
{
    if (!s_gdiplus_ready)
    {
        Gdiplus::GdiplusStartupInput input;
        Gdiplus::GdiplusStartup(&s_gdiplus_token, &input, NULL);
        s_gdiplus_ready = true;
    }
}

void image_decoder_init()
{
    // Deferred — GDI+ is started on first decode to reduce startup memory.
}

void image_decoder_shutdown()
{
    if (s_gdiplus_ready)
    {
        Gdiplus::GdiplusShutdown(s_gdiplus_token);
        s_gdiplus_token = 0;
        s_gdiplus_ready = false;
    }
}

// Convert narrow path to wide string for GDI+.
static std::wstring to_wide(const char *s)
{
    if (!s || !*s)
        return std::wstring();
    int len = MultiByteToWideChar(CP_ACP, 0, s, -1, NULL, 0);
    if (len <= 0)
        return std::wstring();
    std::wstring w(len, L'\0');
    MultiByteToWideChar(CP_ACP, 0, s, -1, &w[0], len);
    w.resize(len - 1); // strip null terminator from the string object
    return w;
}

// Common helper: scale a GDI+ Bitmap and convert to RGBA pixel buffer.
static bool scale_and_convert(Gdiplus::Bitmap *src, int max_w, int max_h, DecodedImage *out)
{
    int src_w = (int)src->GetWidth();
    int src_h = (int)src->GetHeight();
    if (src_w <= 0 || src_h <= 0)
        return false;

    // Compute target size preserving aspect ratio.
    int dst_w = src_w;
    int dst_h = src_h;

    if (dst_w > max_w)
    {
        dst_h = dst_h * max_w / dst_w;
        dst_w = max_w;
    }
    if (dst_h > max_h)
    {
        dst_w = dst_w * max_h / dst_h;
        dst_h = max_h;
    }
    if (dst_w <= 0)
        dst_w = 1;
    if (dst_h <= 0)
        dst_h = 1;

    // Draw scaled version into a 32-bit ARGB bitmap.
    Gdiplus::Bitmap scaled(dst_w, dst_h, PixelFormat32bppARGB);
    {
        Gdiplus::Graphics g(&scaled);
        g.SetInterpolationMode(Gdiplus::InterpolationModeHighQualityBicubic);
        g.DrawImage(src, 0, 0, dst_w, dst_h);
    }

    // Lock bits and copy to our RGBA buffer.
    Gdiplus::Rect rect(0, 0, dst_w, dst_h);
    Gdiplus::BitmapData data;
    if (scaled.LockBits(&rect, Gdiplus::ImageLockModeRead,
                         PixelFormat32bppARGB, &data) != Gdiplus::Ok)
        return false;

    int pixel_count = dst_w * dst_h;
    unsigned char *pixels = (unsigned char *)malloc(pixel_count * 4);
    if (!pixels)
    {
        scaled.UnlockBits(&data);
        return false;
    }

    // GDI+ PixelFormat32bppARGB stores each pixel as BGRA in memory.
    // Convert to RGBA for our output.
    for (int y = 0; y < dst_h; y++)
    {
        const unsigned char *src_row = (const unsigned char *)data.Scan0 + y * data.Stride;
        unsigned char *dst_row = pixels + y * dst_w * 4;
        for (int x = 0; x < dst_w; x++)
        {
            dst_row[x * 4 + 0] = src_row[x * 4 + 2]; // R
            dst_row[x * 4 + 1] = src_row[x * 4 + 1]; // G
            dst_row[x * 4 + 2] = src_row[x * 4 + 0]; // B
            dst_row[x * 4 + 3] = src_row[x * 4 + 3]; // A
        }
    }

    scaled.UnlockBits(&data);

    out->pixels = pixels;
    out->width = dst_w;
    out->height = dst_h;
    return true;
}

bool decode_image_file(const char *path, int max_w, int max_h, DecodedImage *out)
{
    if (!out)
        return false;

    ensure_gdiplus();
    out->pixels = NULL;
    out->width = 0;
    out->height = 0;

    std::wstring wpath = to_wide(path);
    if (wpath.empty())
        return false;

    Gdiplus::Bitmap *src = new Gdiplus::Bitmap(wpath.c_str());
    if (!src || src->GetLastStatus() != Gdiplus::Ok)
    {
        delete src;
        return false;
    }

    bool ok = scale_and_convert(src, max_w, max_h, out);
    delete src;
    return ok;
}

bool decode_image_memory(const void *data, int data_size, int max_w, int max_h, DecodedImage *out)
{
    if (!out || !data || data_size <= 0)
        return false;

    ensure_gdiplus();
    out->pixels = NULL;
    out->width = 0;
    out->height = 0;

    // Create an IStream over the memory buffer.
    HGLOBAL hMem = GlobalAlloc(GMEM_MOVEABLE, data_size);
    if (!hMem)
        return false;

    void *pMem = GlobalLock(hMem);
    memcpy(pMem, data, data_size);
    GlobalUnlock(hMem);

    IStream *stream = NULL;
    if (CreateStreamOnHGlobal(hMem, TRUE, &stream) != S_OK)
    {
        GlobalFree(hMem);
        return false;
    }

    Gdiplus::Bitmap *src = new Gdiplus::Bitmap(stream);
    bool ok = (src && src->GetLastStatus() == Gdiplus::Ok);
    if (ok)
        ok = scale_and_convert(src, max_w, max_h, out);

    delete src;
    stream->Release(); // also frees hMem (fDeleteOnRelease = TRUE)
    return ok;
}

bool decode_image_resource(const char *resource_name, int max_w, int max_h, DecodedImage *out)
{
    if (!resource_name || !out)
        return false;

    HRSRC hRes = FindResourceA(NULL, resource_name, RT_RCDATA);
    if (!hRes)
        return false;
    HGLOBAL hData = LoadResource(NULL, hRes);
    if (!hData)
        return false;
    const void *resData = LockResource(hData);
    int resSize = (int)SizeofResource(NULL, hRes);

    return decode_image_memory(resData, resSize, max_w, max_h, out);
}

void free_decoded_image(DecodedImage *img)
{
    if (img && img->pixels)
    {
        free(img->pixels);
        img->pixels = NULL;
    }
}
