/* stb_impl.c — the single translation unit that instantiates the stb
 * single-header libraries.
 *
 * STBI_NO_STDIO: we always read files ourselves and hand stb a memory
 * buffer, which keeps all filename handling (and its ANSI/wide question) in
 * one place in image_decoder.cpp.
 *
 * STBI_NO_SIMD / STBIR_NO_SIMD are set from CMake under TARGET_WIN9X — both
 * libraries otherwise auto-detect and emit SSE2, which is an illegal opcode
 * on the Pentium/PII/K6-class machines this build targets.
 */

#define STBI_NO_STDIO

/* Formats the launcher does not use. Dropping them shrinks the binary and
 * removes decoders we would never exercise. */
#define STBI_NO_PSD
#define STBI_NO_TGA
#define STBI_NO_HDR
#define STBI_NO_PIC
#define STBI_NO_PNM

#define STB_IMAGE_IMPLEMENTATION
#include "stb_image.h"

#define STB_IMAGE_RESIZE_IMPLEMENTATION
#include "stb_image_resize2.h"

/* stb_truetype backs font_stb.cpp, which is the DOS build's text renderer.
 * Gated so the SDL and Allegro builds -- which rasterise through SDL_ttf and
 * GDI -- do not carry a second, unused rasteriser. */
#ifdef LAUNCHER_FONT_STB
#define STB_TRUETYPE_IMPLEMENTATION
#include "stb_truetype.h"
#endif
