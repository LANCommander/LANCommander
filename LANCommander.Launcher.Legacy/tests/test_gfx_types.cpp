#include "test_main.h"

#include "gfx/gfx.h"

using namespace launcher;

void test_gfx_types()
{
    // --- Colour construction and alpha ---
    {
        gfx::Color c = gfx::rgb(0x12, 0x34, 0x56);
        CHECK_INT(c.r, 0x12);
        CHECK_INT(c.g, 0x34);
        CHECK_INT(c.b, 0x56);
        CHECK_INT(c.a, 255); // rgb() is opaque

        gfx::Color t = gfx::rgba(1, 2, 3, 4);
        CHECK_INT(t.a, 4);

        // with_alpha keeps the channels and replaces only alpha. The
        // game-detail gradient relies on this to fade one colour out.
        gfx::Color faded = gfx::with_alpha(c, 0);
        CHECK_INT(faded.r, 0x12);
        CHECK_INT(faded.g, 0x34);
        CHECK_INT(faded.b, 0x56);
        CHECK_INT(faded.a, 0);
    }

    // --- Rect containment ---
    // This is the predicate every widget hit-test is built on, so its
    // boundary behaviour matters: top-left inclusive, bottom-right exclusive.
    {
        gfx::Rect r = gfx::rect(10, 20, 100, 50);
        CHECK_INT(r.x, 10);
        CHECK_INT(r.y, 20);
        CHECK_INT(r.w, 100);
        CHECK_INT(r.h, 50);

        CHECK(gfx::rect_contains(r, 10, 20));    // top-left corner is inside
        CHECK(gfx::rect_contains(r, 109, 69));   // last pixel inside
        CHECK(!gfx::rect_contains(r, 110, 69));  // one past the right edge
        CHECK(!gfx::rect_contains(r, 109, 70));  // one past the bottom edge
        CHECK(!gfx::rect_contains(r, 9, 20));
        CHECK(!gfx::rect_contains(r, 10, 19));

        // An empty rect contains nothing, including its own origin.
        gfx::Rect empty = gfx::rect(5, 5, 0, 0);
        CHECK(!gfx::rect_contains(empty, 5, 5));
    }

    // --- Clip stack intersects rather than replaces ---
    // The pre-seam code faked save/restore by re-setting a full-screen rect,
    // which silently clobbered an enclosing clip when nested. That bug is
    // what this pins.
    {
        gfx::Surface *s = gfx::create_surface(200, 100);
        CHECK(s != NULL);

        gfx::Rect base = gfx::get_clip(s);
        CHECK_INT(base.w, 200);
        CHECK_INT(base.h, 100);

        gfx::push_clip(s, gfx::rect(0, 0, 50, 50));
        CHECK_INT(gfx::get_clip(s).w, 50);

        // Nested push must not widen past the outer clip.
        gfx::push_clip(s, gfx::rect(0, 0, 500, 500));
        CHECK_INT(gfx::get_clip(s).w, 50);
        CHECK_INT(gfx::get_clip(s).h, 50);

        gfx::pop_clip(s);
        CHECK_INT(gfx::get_clip(s).w, 50);

        gfx::pop_clip(s);
        CHECK_INT(gfx::get_clip(s).w, 200);

        // Popping past the base is a no-op rather than corruption.
        gfx::pop_clip(s);
        CHECK_INT(gfx::get_clip(s).w, 200);

        // Disjoint regions produce an empty clip, not a negative one.
        gfx::push_clip(s, gfx::rect(0, 0, 10, 10));
        gfx::push_clip(s, gfx::rect(100, 100, 10, 10));
        gfx::Rect none = gfx::get_clip(s);
        CHECK(none.w == 0 || none.h == 0);
        CHECK(none.w >= 0 && none.h >= 0);

        gfx::destroy_surface(s);
    }

    // --- Surface dimensions round-trip ---
    {
        gfx::Surface *s = gfx::create_surface(64, 32);
        CHECK_INT(gfx::surface_width(s), 64);
        CHECK_INT(gfx::surface_height(s), 32);
        gfx::destroy_surface(s);

        // Degenerate sizes are refused rather than producing a bad surface.
        CHECK(gfx::create_surface(0, 10) == NULL);
        CHECK(gfx::create_surface(10, -1) == NULL);

        // NULL is tolerated everywhere the screens might pass a failed decode.
        CHECK_INT(gfx::surface_width(NULL), 0);
        CHECK_INT(gfx::surface_height(NULL), 0);
        gfx::destroy_surface(NULL);
    }
}
