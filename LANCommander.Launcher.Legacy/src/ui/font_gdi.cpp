// gdi_font.cpp — TrueType font rendering via GDI, blitted into Allegro
// gfx surfaces via a coverage mask.
//
// Unicode strategy (MSLU / "unicows" pattern):
//   All text input is UTF-8. We always convert to UTF-16 internally.
//   At runtime we detect whether the OS supports W (wide) Win32 APIs:
//     - NT-based (2000+): call W functions directly → full Unicode.
//     - Win9x (95/98/Me): convert UTF-16 → current codepage via
//       WideCharToMultiByte, then call A functions → best-effort rendering.
//   This is the same approach Microsoft's unicows.dll takes, implemented
//   inline for the small set of GDI functions we actually use.

#include "ui/font.h"

#include <windows.h>
#include <cstring>
#include <cstdlib>
#include <string>
#include <vector>

namespace
{
    HFONT g_hfont = NULL;
    HDC g_measure_dc = NULL;   // off-screen DC kept for measuring only
    int g_font_height = 0;
    bool g_wide_ok = false;    // true on NT-based systems (W APIs work)

    // Detect whether the OS supports W (wide/Unicode) Win32 APIs.
    void detect_unicode_support()
    {
        OSVERSIONINFOA ovi;
        ovi.dwOSVersionInfoSize = sizeof(ovi);
        GetVersionExA(&ovi);
        g_wide_ok = (ovi.dwPlatformId == VER_PLATFORM_WIN32_NT);
    }

    // Convert UTF-8 to UTF-16. Works on all Win32 platforms.
    std::wstring utf8_to_wide(const char *s, int len = -1)
    {
        if (!s || (len == 0) || (len == -1 && !*s))
            return std::wstring();
        int wlen = MultiByteToWideChar(CP_UTF8, 0, s, len, NULL, 0);
        if (wlen <= 0)
            return std::wstring();
        std::wstring w(wlen, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, s, len, &w[0], wlen);
        if (len == -1 && !w.empty() && w.back() == L'\0')
            w.pop_back();
        return w;
    }

    // Convert UTF-16 to the current ANSI codepage (for Win9x A-function fallback).
    std::string wide_to_ansi(const std::wstring &w)
    {
        if (w.empty())
            return std::string();
        int alen = WideCharToMultiByte(CP_ACP, 0, w.c_str(), (int)w.size(),
                                       NULL, 0, NULL, NULL);
        if (alen <= 0)
            return std::string();
        std::string a(alen, '\0');
        WideCharToMultiByte(CP_ACP, 0, w.c_str(), (int)w.size(),
                            &a[0], alen, NULL, NULL);
        return a;
    }

    // Try to create the font with the given face name.
    HFONT try_create(const char *face, int pt)
    {
        // Win9x doesn't have ClearType.
        DWORD quality = g_wide_ok ? CLEARTYPE_QUALITY : ANTIALIASED_QUALITY;

        if (g_wide_ok)
        {
            std::wstring wface = utf8_to_wide(face);
            return CreateFontW(
                -pt, 0, 0, 0,
                FW_NORMAL,
                FALSE, FALSE, FALSE,
                DEFAULT_CHARSET,
                OUT_TT_PRECIS,
                CLIP_DEFAULT_PRECIS,
                quality,
                DEFAULT_PITCH | FF_SWISS,
                wface.c_str());
        }
        else
        {
            return CreateFontA(
                -pt, 0, 0, 0,
                FW_NORMAL,
                FALSE, FALSE, FALSE,
                DEFAULT_CHARSET,
                OUT_TT_PRECIS,
                CLIP_DEFAULT_PRECIS,
                quality,
                DEFAULT_PITCH | FF_SWISS,
                face);
        }
    }

    // Verify the returned font actually matches the requested face name
    // (GDI silently substitutes if the face isn't found).
    bool font_matches(HFONT hf, const char *expected)
    {
        HDC dc = GetDC(NULL);
        HFONT old = (HFONT)SelectObject(dc, hf);

        bool match = false;
        if (g_wide_ok)
        {
            wchar_t actual[LF_FACESIZE] = {};
            GetTextFaceW(dc, LF_FACESIZE, actual);
            std::wstring wexpected = utf8_to_wide(expected);
            match = (_wcsicmp(actual, wexpected.c_str()) == 0);
        }
        else
        {
            char actual[LF_FACESIZE] = {};
            GetTextFaceA(dc, LF_FACESIZE, actual);
            match = (_stricmp(actual, expected) == 0);
        }

        SelectObject(dc, old);
        ReleaseDC(NULL, dc);
        return match;
    }

    // Measure text width using the appropriate API.
    void measure_text(HDC dc, const std::wstring &wtext, SIZE *sz)
    {
        if (g_wide_ok)
        {
            GetTextExtentPoint32W(dc, wtext.c_str(), (int)wtext.size(), sz);
        }
        else
        {
            std::string ansi = wide_to_ansi(wtext);
            GetTextExtentPoint32A(dc, ansi.c_str(), (int)ansi.size(), sz);
        }
    }

    // Draw text into a DC using the appropriate API.
    void draw_to_dc(HDC dc, const std::wstring &wtext, RECT *rc, UINT fmt)
    {
        if (g_wide_ok)
        {
            DrawTextW(dc, wtext.c_str(), (int)wtext.size(), rc, fmt);
        }
        else
        {
            std::string ansi = wide_to_ansi(wtext);
            DrawTextA(dc, ansi.c_str(), (int)ansi.size(), rc, fmt);
        }
    }
}

void gdi_font_init(int point_size)
{
    detect_unicode_support();

    // Try Inter first, then Segoe UI, then Arial.
    static const char *faces[] = {"Inter", "Segoe UI", "Arial"};
    for (int i = 0; i < 3; ++i)
    {
        g_hfont = try_create(faces[i], point_size);
        if (g_hfont && font_matches(g_hfont, faces[i]))
            break;
        if (g_hfont)
        {
            DeleteObject(g_hfont);
            g_hfont = NULL;
        }
    }

    // Last resort — let GDI pick anything.
    if (!g_hfont)
        g_hfont = try_create("Arial", point_size);

    // Create a persistent DC for text measurement.
    g_measure_dc = CreateCompatibleDC(NULL);
    SelectObject(g_measure_dc, g_hfont);

    if (g_wide_ok)
    {
        TEXTMETRICW tm;
        GetTextMetricsW(g_measure_dc, &tm);
        g_font_height = tm.tmHeight;
    }
    else
    {
        TEXTMETRICA tm;
        GetTextMetricsA(g_measure_dc, &tm);
        g_font_height = tm.tmHeight;
    }
}

void gdi_font_shutdown()
{
    if (g_measure_dc)
    {
        DeleteDC(g_measure_dc);
        g_measure_dc = NULL;
    }
    if (g_hfont)
    {
        DeleteObject(g_hfont);
        g_hfont = NULL;
    }
}

int gdi_font_height()
{
    return g_font_height;
}

int gdi_font_text_width(const char *text)
{
    if (!g_measure_dc || !text || !*text)
        return 0;
    std::wstring w = utf8_to_wide(text);
    SIZE sz;
    measure_text(g_measure_dc, w, &sz);
    return sz.cx;
}

// -----------------------------------------------------------------------
// Core rendering: rasterise text to a coverage mask via a temporary GDI DIB.
//
// Strategy:
//   1. Create a small DIB section just big enough for the text.
//   2. Fill it with black (so GDI anti-aliasing blends against black).
//   3. Render white text onto the DIB.
//   4. Read pixels back — the white channel gives us coverage (alpha).
//   5. Hand the coverage mask to gfx, which does the composite.
//
// Step 5 used to be an inline getpixel/putpixel loop against an Allegro
// BITMAP. Keeping the composite behind gfx is what lets SDL_ttf drop in
// later without touching any caller.
// -----------------------------------------------------------------------

static void render(launcher::gfx::Surface *dst, int x, int y,
                   launcher::gfx::Color color, const char *text)
{
    if (!g_hfont || !text || !*text || !dst)
        return;

    std::wstring wtext = utf8_to_wide(text);

    SIZE sz;
    measure_text(g_measure_dc, wtext, &sz);
    int tw = sz.cx;
    int th = sz.cy;
    if (tw <= 0 || th <= 0)
        return;

    // Create a 32-bit DIB section.
    BITMAPINFO bmi;
    memset(&bmi, 0, sizeof(bmi));
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = tw;
    bmi.bmiHeader.biHeight = -th; // top-down
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void *bits = NULL;
    HDC dc = CreateCompatibleDC(NULL);
    HBITMAP dib = CreateDIBSection(dc, &bmi, DIB_RGB_COLORS, &bits, NULL, 0);
    if (!dib || !bits)
    {
        DeleteDC(dc);
        return;
    }

    HBITMAP old_bmp = (HBITMAP)SelectObject(dc, dib);
    HFONT old_font = (HFONT)SelectObject(dc, g_hfont);

    // Clear to black.
    memset(bits, 0, tw * th * 4);

    // Draw white text — the red channel (or any channel) gives us coverage.
    SetTextColor(dc, (COLORREF)0x00FFFFFF); // white
    SetBkMode(dc, TRANSPARENT);
    RECT rc = {0, 0, tw, th};
    draw_to_dc(dc, wtext, &rc, DT_LEFT | DT_TOP | DT_NOPREFIX | DT_SINGLELINE);

    GdiFlush();

    // Extract an 8-bit coverage mask from the BGRA DIB. Coverage comes from
    // the red channel — ClearType produces per-channel values, but we
    // flatten to greyscale, as the previous inline blend did.
    std::vector<unsigned char> mask((size_t)tw * th);
    {
        const unsigned char *src = (const unsigned char *)bits;
        unsigned char *out = &mask[0];
        const size_t count = (size_t)tw * th;
        for (size_t i = 0; i < count; ++i)
        {
            out[i] = src[2];
            src += 4;
        }
    }

    launcher::gfx::blend_coverage(dst, x, y, tw, th, &mask[0], tw, color);

    SelectObject(dc, old_font);
    SelectObject(dc, old_bmp);
    DeleteObject(dib);
    DeleteDC(dc);
}

// --- Public API ---

namespace launcher
{
    namespace ui
    {

        bool font_init(int px_size)
        {
            gdi_font_init(px_size);
            return gdi_font_height() > 0;
        }

        void font_shutdown()
        {
            gdi_font_shutdown();
        }

        int font_height()
        {
            return gdi_font_height();
        }

        int font_measure(const char *utf8)
        {
            return gdi_font_text_width(utf8);
        }

        int font_fit(const char *utf8, int max_w, int *out_w)
        {
            if (out_w)
                *out_w = 0;
            if (!g_measure_dc || !utf8 || !*utf8 || max_w <= 0)
                return 0;

            std::wstring w = utf8_to_wide(utf8);
            if (w.empty())
                return 0;

            HFONT old = (HFONT)SelectObject(g_measure_dc, g_hfont);

            INT fit = 0;
            SIZE sz = { 0, 0 };
            BOOL ok;

            if (g_wide_ok)
            {
                ok = GetTextExtentExPointW(g_measure_dc, w.c_str(), (int)w.size(),
                                           max_w, &fit, NULL, &sz);
            }
            else
            {
                std::string a = wide_to_ansi(w);
                ok = GetTextExtentExPointA(g_measure_dc, a.c_str(), (int)a.size(),
                                           max_w, &fit, NULL, &sz);
            }

            SelectObject(g_measure_dc, old);

            if (!ok || fit <= 0)
                return 0;

            // `fit` counts UTF-16 units (or ANSI bytes); callers index into the
            // original UTF-8, so convert the fitting prefix back to get a byte
            // count they can use.
            int bytes;
            if (g_wide_ok)
            {
                bytes = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), fit,
                                            NULL, 0, NULL, NULL);
            }
            else
            {
                // The ANSI path is lossy anyway; treat bytes as 1:1.
                bytes = fit;
            }

            if (out_w)
                *out_w = (int)sz.cx;
            return bytes > 0 ? bytes : 0;
        }

        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8)
        {
            render(dst, x, y, color, utf8);
        }

    } // namespace ui
} // namespace launcher
