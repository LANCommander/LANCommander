/* font-probe.c — calibration + link probe for the SDL_ttf text stack.
 *
 * Two jobs:
 *
 * 1. Prove that a *surface-only* SDL_ttf consumer links against an SDL3 built
 *    with SDL_RENDER / SDL_GPU disabled. SDL_ttf's archive references
 *    SDL_RenderGeometryRaw and SDL_CreateGPUTexture from its renderer and GPU
 *    text engines; if we never call those engines the linker should not pull
 *    those objects in. This program is the proof.
 *
 * 2. Measure the font-metric shift between the old GDI path and SDL_ttf.
 *    Every layout in the launcher derives from text_height(), which today is
 *    GDI's TEXTMETRIC.tmHeight for CreateFontA(-13, ...). TTF_GetFontHeight()
 *    uses different semantics, so the launcher needs the ptsize that makes the
 *    two match. This sweeps ptsize and prints the comparison.
 *
 * Usage: font-probe <font.ttf> [sample text]
 */

#include <SDL3/SDL.h>
#include <SDL3_ttf/SDL_ttf.h>

#include <stdio.h>

#ifdef _WIN32
#include <windows.h>

/* Mirrors gdi_font.cpp: CreateFontA(-point_size, ...) then TEXTMETRIC. */
static int gdi_metrics(int point_size, const char *face,
                       const char *sample, int *out_width)
{
    HDC dc = CreateCompatibleDC(NULL);
    HFONT f, old;
    TEXTMETRICA tm;
    SIZE sz;
    int height;

    if (!dc)
        return 0;

    f = CreateFontA(-point_size, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                    DEFAULT_CHARSET, OUT_TT_PRECIS, CLIP_DEFAULT_PRECIS,
                    ANTIALIASED_QUALITY, DEFAULT_PITCH | FF_DONTCARE, face);
    if (!f) {
        DeleteDC(dc);
        return 0;
    }

    old = (HFONT)SelectObject(dc, f);
    GetTextMetricsA(dc, &tm);
    height = (int)tm.tmHeight;

    GetTextExtentPoint32A(dc, sample, (int)strlen(sample), &sz);
    *out_width = (int)sz.cx;

    SelectObject(dc, old);
    DeleteObject(f);
    DeleteDC(dc);
    return height;
}
#endif

int main(int argc, char **argv)
{
    const char *path;
    const char *sample = "Downloads (3) — LANCommander";
    int pt;

    if (argc < 2) {
        fprintf(stderr, "usage: %s <font.ttf> [sample text]\n", argv[0]);
        return 2;
    }
    path = argv[1];
    if (argc > 2)
        sample = argv[2];

    if (!TTF_Init()) {
        fprintf(stderr, "TTF_Init failed: %s\n", SDL_GetError());
        return 1;
    }

    printf("sample: \"%s\"\n\n", sample);

#ifdef _WIN32
    {
        /* gdi_font.cpp asks for Inter first and falls back Segoe UI -> Arial,
         * so measure the whole chain: whichever face the target machine has
         * is the layout the SDL_ttf path has to match. */
        static const char *faces[] = { "Inter", "Segoe UI", "Arial" };
        size_t i;

        printf("GDI baseline  CreateFontA(-13, face)\n");
        printf("  %-12s %-12s %-12s\n", "face", "tmHeight", "width");
        for (i = 0; i < sizeof(faces) / sizeof(faces[0]); ++i) {
            int gw = 0;
            int gh = gdi_metrics(13, faces[i], sample, &gw);
            printf("  %-12s %-12d %-12d\n", faces[i], gh, gw);
        }
        printf("\n");
    }
#endif

    printf("SDL_ttf TTF_OpenFont(\"%s\", ptsize)\n", path);
    printf("  %-8s %-12s %-12s %-12s\n",
           "ptsize", "FontHeight", "Ascent", "StringWidth");

    for (pt = 9; pt <= 20; ++pt) {
        TTF_Font *font = TTF_OpenFont(path, (float)pt);
        int w = 0, h = 0;

        if (!font) {
            printf("  %-8d (open failed: %s)\n", pt, SDL_GetError());
            continue;
        }

        TTF_GetStringSize(font, sample, 0, &w, &h);
        printf("  %-8d %-12d %-12d %-12d\n",
               pt, TTF_GetFontHeight(font), TTF_GetFontAscent(font), w);

        TTF_CloseFont(font);
    }

    /* Render once to confirm the surface path works end to end without any
     * renderer or GPU backend present. */
    {
        TTF_Font *font = TTF_OpenFont(path, 13.0f);
        SDL_Color white = { 255, 255, 255, 255 };
        SDL_Surface *s;

        if (!font) {
            fprintf(stderr, "\nTTF_OpenFont failed: %s\n", SDL_GetError());
            TTF_Quit();
            return 1;
        }

        s = TTF_RenderText_Blended(font, sample, 0, white);
        if (!s) {
            fprintf(stderr, "\nTTF_RenderText_Blended failed: %s\n", SDL_GetError());
            TTF_CloseFont(font);
            TTF_Quit();
            return 1;
        }

        printf("\nsurface render OK: %dx%d, format %s\n",
               s->w, s->h, SDL_GetPixelFormatName(s->format));

        SDL_DestroySurface(s);
        TTF_CloseFont(font);
    }

    TTF_Quit();
    return 0;
}
