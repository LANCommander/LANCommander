// font_fake.cpp — deterministic font metrics for tests.
//
// Every glyph is FAKE_ADVANCE pixels wide and the line height is fixed, so
// expected wrap positions can be written down by hand and stay stable
// regardless of what font a machine happens to have installed. Testing the
// wrap algorithm against a real rasteriser would only prove that FreeType is
// consistent with itself.
//
// Not selectable as a launcher backend — linked only by launcher_tests.

#include "ui/font.h"

#include <cstring>
#include <string>
#include <vector>

namespace launcher
{
    namespace ui
    {

        // Chosen as a non-trivial value: 1 would let off-by-one errors in the
        // pixels-to-characters arithmetic pass unnoticed.
        //
        // `extern` because a namespace-scope const has internal linkage in
        // C++, and the tests reference these to express expectations in terms
        // of the metrics rather than hardcoding 10 and 16.
        extern const int FAKE_ADVANCE = 10;
        extern const int FAKE_HEIGHT = 16;

        namespace
        {
            // The fake metrics are the Body rung's, and the other rungs are
            // scaled off it by the same ratio font_px() gives. Body divides
            // out exactly, so every existing expectation written in terms of
            // FAKE_ADVANCE / FAKE_HEIGHT still holds — only a test that opts
            // into another rung sees anything different.
            const int FAKE_BASE_PX = 13;

            int scaled(int metric, FontSize size)
            {
                return metric * font_px(size) / FAKE_BASE_PX;
            }
        } // namespace

        bool font_init()
        {
            return true;
        }

        void font_shutdown() {}

        int font_height(FontSize size) { return scaled(FAKE_HEIGHT, size); }

        int font_measure(const char *utf8, FontSize size)
        {
            if (!utf8)
                return 0;
            return (int)std::strlen(utf8) * scaled(FAKE_ADVANCE, size);
        }

        int font_fit(const char *utf8, int max_w, int *out_w, FontSize size)
        {
            if (out_w)
                *out_w = 0;

            const int advance = scaled(FAKE_ADVANCE, size);
            if (!utf8 || !*utf8 || max_w <= 0 || advance <= 0)
                return 0;

            const int len = (int)std::strlen(utf8);
            int fit = max_w / advance;
            if (fit > len)
                fit = len;

            if (out_w)
                *out_w = fit * advance;
            return fit;
        }

        // Test hook: records what was drawn, so word wrap can be asserted on
        // the actual line breaks rather than inferred from a total height.
        std::vector<std::string> &font_fake_drawn()
        {
            static std::vector<std::string> drawn;
            return drawn;
        }

        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8, FontSize size)
        {
            (void)dst; (void)x; (void)y; (void)color; (void)size;
            if (utf8)
                font_fake_drawn().push_back(utf8);
        }

    } // namespace ui
} // namespace launcher
